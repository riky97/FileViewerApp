using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FileViewerApp.Models;
using FileViewerApp.Enums;

namespace FileViewerApp.Converters
{
    /// <summary>
    /// Converte InstructionDiffKind in colore per gutter laterale.
    /// (Placeholder rimosso)
    /// </summary>
    public class DiffKindToBrushConverter : IValueConverter
    {
        private static readonly IBrush Unchanged = Brushes.Transparent;
        private static readonly IBrush Added = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Verde Material
        private static readonly IBrush Modified = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Giallo/Ambra
        private static readonly IBrush CutPending = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Grigio

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                InstructionDiffKind kind => kind switch
                {
                    InstructionDiffKind.Added => Added,
                    InstructionDiffKind.Modified => Modified,
                    InstructionDiffKind.CutPending => CutPending,
                    _ => Unchanged
                },
                EditableInstruction ei => ei.DiffKind switch
                {
                    InstructionDiffKind.Added => Added,
                    InstructionDiffKind.Modified => Modified,
                    InstructionDiffKind.CutPending => CutPending,
                    _ => Unchanged
                },
                _ => Unchanged
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
