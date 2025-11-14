using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileViewerApp.Converters
{
    public class CutPendingToOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return 0.4; // semi-transparent when cut pending
            return 1.0; // normal opacity
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
