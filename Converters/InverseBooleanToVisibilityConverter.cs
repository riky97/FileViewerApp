using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileViewerApp.Converters
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public static readonly InverseBooleanToVisibilityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            if (value is bool?)
                return (bool?)value != true;
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }
    }
}