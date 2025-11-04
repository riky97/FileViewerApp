using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Service per il caricamento e l'accesso alle risorse (RESGROUP) definite dai file CSV in cartella Resources.
    /// Ora indicizza anche per file: chiave = nome file senza estensione (minuscolo) -> indice con lookup per id.
    /// </summary>
    public class ResourceService
    {
        private readonly Dictionary<string, List<ResourceItem>> _groups = new(StringComparer.OrdinalIgnoreCase); // chiave: nome file normalizzato
        private readonly Dictionary<string, ResourceFileIndex> _fileIndexes = new(StringComparer.OrdinalIgnoreCase); // chiave: filename senza estensione lower
        private readonly Dictionary<string, string> _normalizedNameMap = new(StringComparer.OrdinalIgnoreCase)
        {
            {"BOOL","Bool"},
            {"TIMEOUT","Timeouts"},
            {"TIMEOUTS","Timeouts"},
            {"COSTANTI","COSTANTI"},
            {"COSTANTI_ATM","COSTANTI_ATM"},
            {"CMD_VELOCITA","Cmd_Velocita"},
            {"CMD_LOGICA","Cmd_Logica"},
            {"CMD_ATM","Cmd_ATM"},
            {"CMD_IF","Cmd_If"},
            {"CMD_JOB","Cmd_job"},
            {"CMD_OP","Cmd_Op"},
            {"WR_EVENT","WR_EVENT"},
            {"SINC_TRASLO_CLIENT","SYNC"},
            {"ZONE","Zone"},
        };

        private readonly HashSet<string> _loadedFiles = new();
        private string _resourcesRoot;
        public IReadOnlyDictionary<string, List<ResourceItem>> Groups => _groups;
        public IReadOnlyDictionary<string, ResourceFileIndex> FileIndexes => _fileIndexes;

        public ResourceService(string resourcesRoot)
        {
            _resourcesRoot = resourcesRoot ?? throw new ArgumentNullException(nameof(resourcesRoot));
            if (!Directory.Exists(_resourcesRoot))
                throw new DirectoryNotFoundException($"Resources directory non trovata: {_resourcesRoot}");
            LoadAllCsv();
            BuildFileIndexes();
        }

        private void LoadAllCsv()
        {
            foreach (var file in Directory.EnumerateFiles(_resourcesRoot, "*.csv", SearchOption.TopDirectoryOnly))
            {
                try { LoadCsv(file); } catch { /* log eventuale */ }
            }
        }

        private void LoadCsv(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path); // es: MEM
            if (_loadedFiles.Contains(path)) return;
            _loadedFiles.Add(path);

            // Normalizza group name da filename (resta concetto "gruppo" associato al file intero)
            var groupKey = NormalizeGroupName(fileName); // es: MEM -> MEM
            var list = _groups.TryGetValue(groupKey, out var existing) ? existing : (_groups[groupKey] = new List<ResourceItem>());

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith('#')) continue; // commenti

                var parts = line.Split(';');
                if (parts.Length < 3) continue; // minimo: cat/name;value

                string first = parts[0];
                string second = parts[1];
                string value = parts[2];
                string? description = parts.Length > 3 ? parts[3] : null;

                string? category;
                string name;
                if (string.IsNullOrWhiteSpace(first))
                {
                    category = null;
                    name = second;
                }
                else
                {
                    category = first.Trim();
                    name = second.Trim();
                }

                var normalizedValue = value.Replace(',', '.').Trim();
                int? intVal = null; double? floatVal = null;
                if (int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                {
                    intVal = iv;
                }
                else if (double.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                {
                    floatVal = dv;
                }

                // Filtra descrizioni placeholder
                if (!string.IsNullOrWhiteSpace(description) && string.Equals(description.Trim(), "text", StringComparison.OrdinalIgnoreCase))
                    description = null;

                var item = new ResourceItem(groupKey, category, name, value, intVal, floatVal, description);
                list.Add(item);
            }
        }

        private void BuildFileIndexes()
        {
            _fileIndexes.Clear();
            foreach (var kv in _groups)
            {
                var fileKeyLower = kv.Key.ToLowerInvariant();
                var entries = kv.Value;
                var byId = new Dictionary<int, ResourceItem>();
                foreach (var e in entries)
                {
                    if (e.IntValue.HasValue && !byId.ContainsKey(e.IntValue.Value))
                        byId[e.IntValue.Value] = e;
                }
                _fileIndexes[fileKeyLower] = new ResourceFileIndex(kv.Key, entries, byId);
            }
        }

        private string NormalizeGroupName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var upper = raw.ToUpperInvariant();
            return _normalizedNameMap.TryGetValue(upper, out var mapped) ? mapped : raw; // conserva forma originale se non mappato
        }

        // === API per file index ===
        public bool FileExists(string fileKey) => _fileIndexes.ContainsKey(fileKey.ToLowerInvariant());
        public ResourceFileIndex? GetFileIndex(string fileKey)
        {
            _fileIndexes.TryGetValue(fileKey.ToLowerInvariant(), out var idx);
            return idx;
        }
        public ResourceItem? TryGetById(string fileKey, int id)
        {
            var idx = GetFileIndex(fileKey);
            if (idx == null) return null;
            return idx.ById.TryGetValue(id, out var item) ? item : null;
        }
        public IReadOnlyCollection<ResourceItem> GetAll(string fileKey)
        {
            var idx = GetFileIndex(fileKey);
            return idx?.Entries ?? Array.Empty<ResourceItem>();
        }
        public IEnumerable<ResourceItem> Search(string fileKey, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return GetAll(fileKey);
            text = text.Trim();
            return GetAll(fileKey).Where(e => (e.Description ?? e.Name).Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        // === API legacy basate su "group" (file) ===
        public IEnumerable<ResourceItem> GetGroup(string group)
        {
            if (_groups.TryGetValue(group, out var list)) return list;
            return Enumerable.Empty<ResourceItem>();
        }
        public ResourceItem? FindByName(string group, string name)
        {
            if (!_groups.TryGetValue(group, out var list)) return null;
            return list.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public ResourceItem? FindByValue(string group, int value)
        {
            if (!_groups.TryGetValue(group, out var list)) return null;
            return list.FirstOrDefault(i => i.IntValue == value);
        }
        public ResourceItem? FindByValue(string group, double value, double tolerance = 1e-6)
        {
            if (!_groups.TryGetValue(group, out var list)) return null;
            return list.FirstOrDefault(i => i.FloatValue.HasValue && Math.Abs(i.FloatValue.Value - value) <= tolerance);
        }
        public bool GroupExists(string group) => _groups.ContainsKey(group);
        public IEnumerable<string> AllGroupNames() => _groups.Keys.OrderBy(k => k);

        public ResourceValidationResult ValidateAgainstInfoXml(string infoXmlPath)
        {
            if (!File.Exists(infoXmlPath)) throw new FileNotFoundException("INFO.XML non trovato", infoXmlPath);
            var xml = File.ReadAllText(infoXmlPath);
            var xmlGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var sr = new StringReader(xml))
            {
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    var idx = line.IndexOf("<RESGROUP", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    var nameIdx = line.IndexOf("Name=\"", idx, StringComparison.OrdinalIgnoreCase);
                    if (nameIdx < 0) continue;
                    nameIdx += 6;
                    var endIdx = line.IndexOf('"', nameIdx);
                    if (endIdx < 0) continue;
                    var groupName = line.Substring(nameIdx, endIdx - nameIdx).Trim();
                    if (groupName.Length > 0)
                        xmlGroups.Add(groupName);
                }
            }
            var loadedGroups = new HashSet<string>(_groups.Keys, StringComparer.OrdinalIgnoreCase);
            var missing = xmlGroups.Where(g => !loadedGroups.Contains(g)).OrderBy(g => g).ToList();
            var unused = loadedGroups.Where(g => !xmlGroups.Contains(g)).OrderBy(g => g).ToList();
            var counts = _groups.ToDictionary(k => k.Key, v => v.Value.Count, StringComparer.OrdinalIgnoreCase);
            return new ResourceValidationResult(xmlGroups.ToList(), loadedGroups.ToList(), missing, unused, counts);
        }

        // === Opzioni per UI ===
        public IEnumerable<ResourceOption> GetOptions(string group)
        {
            return GetGroup(group).Select(g => new ResourceOption(g));
        }
        public IEnumerable<ResourceOption> GetCombinedOptions(IEnumerable<string> groups)
        {
            var seen = new HashSet<int>();
            foreach (var g in groups)
            {
                foreach (var item in GetGroup(g))
                {
                    if (item.IntValue.HasValue && !seen.Add(item.IntValue.Value)) continue;
                    yield return new ResourceOption(item);
                }
            }
        }
        public IEnumerable<ResourceOption> FilterOptions(IEnumerable<ResourceOption> source, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return source;
            filter = filter.Trim();
            bool numeric = int.TryParse(filter, out var num);
            return source.Where(o =>
                (numeric && o.Id == num) ||
                o.Display.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Indice per un singolo file di risorse (chiave = nome file senza estensione)
    /// </summary>
    public sealed class ResourceFileIndex
    {
        public string FileKey { get; }
        public IReadOnlyList<ResourceItem> Entries { get; }
        public IReadOnlyDictionary<int, ResourceItem> ById { get; }
        public ResourceFileIndex(string fileKey, List<ResourceItem> entries, Dictionary<int, ResourceItem> byId)
        {
            FileKey = fileKey;
            Entries = entries;
            ById = byId;
        }
    }
}
