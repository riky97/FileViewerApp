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
                        Console.WriteLine($"Caricati {_opCodeInfos.Count} OpCodes da {configFile}");
                        return _opCodeInfos;
                    }
                }

                // Usa il fallback
                Console.WriteLine("Nessun file di configurazione trovato, usando definizioni di fallback");
                var fallbackLoader = _loaders.OfType<FallbackOpCodeLoader>().First();
                _opCodeInfos = await fallbackLoader.LoadAsync("");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento OpCodes: {ex.Message}");
                var fallbackLoader = _loaders.OfType<FallbackOpCodeLoader>().First();
                _opCodeInfos = await fallbackLoader.LoadAsync("");
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
            }
            else
            {
                throw new NotSupportedException($"Nessun loader disponibile per il file: {filePath}");
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
    }
}