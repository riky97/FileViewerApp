using System;

namespace FileViewerApp.Models
{
    /// <summary>
    /// Rappresenta una singola voce caricata dai file CSV di risorse.
    /// </summary>
    public sealed class ResourceItem
    {
        public string Group { get; }
        public string? Category { get; }
        public string Name { get; }
        public string RawValue { get; }
        public int? IntValue { get; }
        public double? FloatValue { get; }
        public string? Description { get; }

        public ResourceItem(string group, string? category, string name, string rawValue, int? intValue, double? floatValue, string? description)
        {
            Group = group;
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            Name = name.Trim();
            RawValue = rawValue.Trim();
            IntValue = intValue;
            FloatValue = floatValue;
            // normalizza descrizione eliminando placeholder generici
            if (!string.IsNullOrWhiteSpace(description) && !string.Equals(description.Trim(), "text", StringComparison.OrdinalIgnoreCase))
                Description = description.Trim();
        }

        public override string ToString() => IntValue.HasValue ? $"{Group}:{Name}={IntValue}" : $"{Group}:{Name}={RawValue}";
    }

    public sealed class ResourceOption
    {
        public int? Id { get; }
        public string Display { get; }
        public string Group { get; }
        public string? Category { get; }
        public ResourceItem Item { get; }
        public string BaseText { get; }
        public ResourceOption(ResourceItem item)
        {
            Item = item;
            Group = item.Group;
            Category = item.Category;
            Id = item.IntValue;
            // testo base: descrizione se presente altrimenti nome (+ categoria se significativa)
            var main = !string.IsNullOrWhiteSpace(item.Description) ? item.Description : item.Name;
            if (!string.IsNullOrWhiteSpace(item.Category) && !string.Equals(item.Category, Group, StringComparison.OrdinalIgnoreCase))
                main = item.Category + " - " + main;
            BaseText = main;
            Display = Id.HasValue ? $"{main} ({Id})" : main;
        }
        public override string ToString() => Display;
    }
}
