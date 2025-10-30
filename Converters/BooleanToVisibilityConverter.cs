using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileViewerApp.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b;
            if (value is bool?)
                return (bool?)value == true;
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b;
            return false;
        }
    }
}