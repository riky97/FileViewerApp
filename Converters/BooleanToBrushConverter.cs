using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FileViewerApp.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public IBrush TrueBrush { get; set; } = Brushes.ForestGreen;
        public IBrush FalseBrush { get; set; } = Brushes.DarkRed;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? TrueBrush : FalseBrush;
            return FalseBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}