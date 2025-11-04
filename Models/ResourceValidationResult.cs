using System.Collections.Generic;

namespace FileViewerApp.Models
{
    public sealed class ResourceValidationResult
    {
        public IReadOnlyCollection<string> XmlGroups { get; }
        public IReadOnlyCollection<string> LoadedGroups { get; }
        public IReadOnlyCollection<string> MissingGroups { get; }
        public IReadOnlyCollection<string> UnusedGroups { get; }
        public IReadOnlyDictionary<string,int> GroupItemCounts { get; }

        public ResourceValidationResult(
            IReadOnlyCollection<string> xmlGroups,
            IReadOnlyCollection<string> loadedGroups,
            IReadOnlyCollection<string> missingGroups,
            IReadOnlyCollection<string> unusedGroups,
            IReadOnlyDictionary<string,int> groupItemCounts)
        {
            XmlGroups = xmlGroups;
            LoadedGroups = loadedGroups;
            MissingGroups = missingGroups;
            UnusedGroups = unusedGroups;
            GroupItemCounts = groupItemCounts;
        }
    }
}
