using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FileViewerApp.Models; // per InstructionParameter

namespace FileViewerApp.Converters
{
    public class SelectedParamIndexToBrushConverter : IValueConverter
    {
        // Azzurro default (stile simile ai bottoni) e Verde selezionato
        public IBrush SelectedBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32)); // verde
        public IBrush NormalBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x88, 0xE5)); // azzurro/material blue 600

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value può essere l'InstructionParameter (se binding al Self) oppure l'indice
            if (parameter is InstructionParameter ip)
                return ip.IsSelected ? SelectedBrush : NormalBrush;
            if (value is InstructionParameter ip2)
                return ip2.IsSelected ? SelectedBrush : NormalBrush;
            return NormalBrush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
