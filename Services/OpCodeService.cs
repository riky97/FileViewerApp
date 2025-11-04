using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileViewerApp.Interfaces;
using FileViewerApp.Models;
using FileViewerApp.Services;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Servizio principale per gestire OpCodes con supporto multi-formato
    /// </summary>
    public class OpCodeService
    {
        private Dictionary<int, OpCodeInfo> _opCodeInfos = new();
        private readonly List<IOpCodeLoader> _loaders;

        // Evento notificatore quando le definizioni sono state caricate
        public event EventHandler? DefinitionsLoaded;

        private readonly ResourceService _resourceService = new ResourceService(Path.Combine(AppContext.BaseDirectory, "Resources"));
        private readonly Dictionary<int, Dictionary<int, List<string>>> _opcodeParamGroups = new(); // op -> paramIdx -> groups
        private readonly Dictionary<string, Dictionary<int, List<string>>> _opcodeNameParamGroups = new(StringComparer.OrdinalIgnoreCase); // name -> paramIdx -> groups

        public OpCodeService()
        {
            // Registra i loader in ordine di priorità
            _loaders = new List<IOpCodeLoader>
            {
                new XmlOpCodeLoader(),      // Priorità 1: XML (futuro)
                new CfgOpCodeLoader(),      // Priorità 2: CFG (attuale)
                new FallbackOpCodeLoader()  // Priorità 3: Fallback
            };
        }

        /// <summary>
        /// Carica le definizioni degli OpCode automaticamente
        /// </summary>
        public async Task<Dictionary<int, OpCodeInfo>> LoadOpCodeDefinitionsAsync()
        {
            if (_opCodeInfos.Count > 0)
                return _opCodeInfos; // Cache già caricata

            try
            {
                var configFile = FindConfigFile();

                if (configFile != null)
                {
                    // Trova il loader appropriato
                    var loader = _loaders.FirstOrDefault(l => l.CanHandle(configFile));

                    if (loader != null && loader is not FallbackOpCodeLoader)
                    {
                        Console.WriteLine($"Caricamento OpCodes con {loader.LoaderName}: {configFile}");
                        _opCodeInfos = await loader.LoadAsync(configFile);
                        // Parse gruppi parametri se XML
                        if (loader is XmlOpCodeLoader)
                        {
                            try { ParseXmlParamGroups(configFile); } catch (Exception ex) { Console.WriteLine($"ParseXmlParamGroups errore: {ex.Message}"); }
                        }
                        Console.WriteLine($"Caricati {_opCodeInfos.Count} OpCodes da {configFile}");
                        DefinitionsLoaded?.Invoke(this, EventArgs.Empty);
                        // NON fare return anticipato; prosegue per eventuali altre inizializzazioni
                    }
                }

                // Se non era XML o non trovato file, gestito sotto.
                if (_opCodeInfos.Count == 0)
                {
                    Console.WriteLine("Nessun file di configurazione valido caricato, usando definizioni di fallback");
                    var fallbackLoader = _loaders.OfType<FallbackOpCodeLoader>().First();
                    _opCodeInfos = await fallbackLoader.LoadAsync("");
                    DefinitionsLoaded?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento OpCodes: {ex.Message}");
                var fallbackLoader = _loaders.OfType<FallbackOpCodeLoader>().First();
                _opCodeInfos = await fallbackLoader.LoadAsync("");
                DefinitionsLoaded?.Invoke(this, EventArgs.Empty);
            }

            var infoXml = FindConfigFile();
            if (infoXml != null && infoXml.EndsWith("INFO.XML", StringComparison.OrdinalIgnoreCase))
            {
                // Se non già parsato (fallback scenario) prova a parsare
                if (_opcodeParamGroups.Count == 0)
                {
                    try { ParseXmlParamGroups(infoXml); } catch (Exception ex) { Console.WriteLine($"ParseXmlParamGroups (late) errore: {ex.Message}"); }
                }
            }

            return _opCodeInfos;
        }

        /// <summary>
        /// Cerca file di configurazione nelle location standard
        /// </summary>
        private string? FindConfigFile()
        {
            var searchPaths = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "Data"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources")
            };

            var configFiles = new[]
            {
                "INFO.XML",      // Priorità 1: XML futuro
                "INFO.CFG",         // Priorità 2: CFG attuale
                "opcodes.cfg"       // Priorità 3: CFG alternativo
            };

            foreach (var basePath in searchPaths)
            {
                foreach (var fileName in configFiles)
                {
                    var fullPath = Path.Combine(basePath, fileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Carica OpCodes da un file specifico
        /// </summary>
        public async Task LoadFromFileAsync(string filePath)
        {
            var loader = _loaders.FirstOrDefault(l => l.CanHandle(filePath));

            if (loader != null)
            {
                Console.WriteLine($"Caricamento manuale con {loader.LoaderName}: {filePath}");
                _opCodeInfos = await loader.LoadAsync(filePath);
                Console.WriteLine($"Caricati {_opCodeInfos.Count} OpCodes");
                DefinitionsLoaded?.Invoke(this, EventArgs.Empty);

                if (loader is XmlOpCodeLoader)
                {
                    try { ParseXmlParamGroups(filePath); } catch { }
                }
            }
            else
            {
                throw new NotSupportedException($"Nessun loader disponibile per il file: {filePath}");
            }
        }

        private void ParseXmlParamGroups(string xmlPath)
        {
            if (!File.Exists(xmlPath)) return;
            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            _opcodeParamGroups.Clear();
            _opcodeNameParamGroups.Clear();
            foreach (var cmd in doc.Root!.Elements("CMD"))
            {
                var idAttr = cmd.Attribute("ID");
                var nameAttr = cmd.Attribute("Name");
                int? opId = null;
                if (idAttr != null && int.TryParse(idAttr.Value, out var parsed)) opId = parsed;
                var nameKey = nameAttr?.Value ?? string.Empty;
                var nameDict = new Dictionary<int, List<string>>();
                int paramIndex = 0;
                foreach (var par in cmd.Elements("PAR"))
                {
                    var groups = par.Elements("RESGROUP").Select(r => r.Attribute("Name")?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (groups.Count > 0)
                    {
                        nameDict[paramIndex] = groups;
                        if (opId.HasValue)
                        {
                            if (!_opcodeParamGroups.TryGetValue(opId.Value, out var idDict))
                            {
                                idDict = new Dictionary<int, List<string>>();
                                _opcodeParamGroups[opId.Value] = idDict;
                            }
                            idDict[paramIndex] = groups;
                        }
                    }
                    paramIndex++;
                }
                if (!string.IsNullOrWhiteSpace(nameKey) && nameDict.Count > 0)
                {
                    _opcodeNameParamGroups[nameKey] = nameDict;
                }
            }
        }

        public OpCodeInfo? GetOpCodeInfo(int opCode)
        {
            return _opCodeInfos.TryGetValue(opCode, out var info) ? info : null;
        }

        public Dictionary<int, OpCodeInfo> GetAllOpCodes()
        {
            return new Dictionary<int, OpCodeInfo>(_opCodeInfos);
        }

        /// <summary>
        /// Ottiene le statistiche dei loader disponibili
        /// </summary>
        public List<string> GetAvailableLoaders()
        {
            return _loaders.Select(l => l.LoaderName).ToList();
        }

        /// <summary>
        /// Trova un OpCode per nome
        /// </summary>
        public OpCodeInfo? GetOpCodeByName(string name)
        {
            return _opCodeInfos.Values.FirstOrDefault(info =>
                string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Ottiene tutti i nomi degli OpCodes disponibili
        /// </summary>
        public List<string> GetAllOpCodeNames()
        {
            return _opCodeInfos.Values.Select(info => info.Name).OrderBy(name => name).ToList();
        }

        public ResourceService GetResourceService() => _resourceService;

        public IReadOnlyList<string> GetParamResourceGroups(int opCode, int paramIndex)
        {
            if (_opcodeParamGroups.TryGetValue(opCode, out var d) && d.TryGetValue(paramIndex, out var g)) return g;
            return Array.Empty<string>();
        }
        public IReadOnlyList<string> GetParamResourceGroupsByName(string name, int paramIndex)
        {
            if (_opcodeNameParamGroups.TryGetValue(name, out var d) && d.TryGetValue(paramIndex, out var g)) return g;
            return Array.Empty<string>();
        }
        public IEnumerable<ResourceOption> GetParamOptions(int opCode, int paramIndex)
        {
            var groups = GetParamResourceGroups(opCode, paramIndex);
            if (groups.Count == 0) return Enumerable.Empty<ResourceOption>();
            return ResolveGroupsToOptions(groups);
        }
        public IEnumerable<ResourceOption> GetParamOptionsByName(string name, int paramIndex)
        {
            var groups = GetParamResourceGroupsByName(name, paramIndex);
            if (groups.Count == 0) return Enumerable.Empty<ResourceOption>();
            return ResolveGroupsToOptions(groups);
        }

        private IEnumerable<ResourceOption> ResolveGroupsToOptions(IReadOnlyList<string> groups)
        {
            var rs = _resourceService;
            var seen = new HashSet<int>();
            foreach (var g in groups)
            {
                // prima prova gruppo diretto (case-insensitive) su file index key
                var idx = rs.GetFileIndex(g);
                if (idx == null)
                {
                    // fallback: prova lowercase
                    idx = rs.GetFileIndex(g.ToLowerInvariant());
                }
                if (idx == null)
                {
                    // fallback: prova normalizzare underscore/spazi
                    var alt = g.Replace("_", "").Replace(" ", "");
                    idx = rs.GetFileIndex(alt);
                }
                if (idx == null) continue;
                foreach (var item in idx.Entries)
                {
                    if (item.IntValue.HasValue && !seen.Add(item.IntValue.Value)) continue;
                    yield return new ResourceOption(item);
                }
            }
        }

        public IEnumerable<ResourceOption> FilterParamOptions(int opCode, int paramIndex, string filter)
        {
            return _resourceService.FilterOptions(GetParamOptions(opCode, paramIndex), filter);
        }
        public IEnumerable<ResourceOption> FilterParamOptionsByName(string name, int paramIndex, string filter)
        {
            return _resourceService.FilterOptions(GetParamOptionsByName(name, paramIndex), filter);
        }
    }
}