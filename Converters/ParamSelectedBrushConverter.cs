using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FileViewerApp.Converters
{
    public class ParamSelectedBrushConverter : IValueConverter
    {
        public IBrush SelectedBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32)); // verde
        public IBrush NormalBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x88, 0xE5)); // azzurro
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? SelectedBrush : NormalBrush;
            return NormalBrush;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}