using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileViewerApp.Converters
{
    /// <summary>
    /// Converte bool -> double (opacity). true = 1.0, false = 0.0 (con parametro "invert" per invertire).
    /// </summary>
    public class BooleanToOpacityConverter : IValueConverter
    {
        public double TrueOpacity { get; set; } = 1.0;
        public double FalseOpacity { get; set; } = 0.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
            if (value is bool b)
            {
                var result = b ? TrueOpacity : FalseOpacity;
                if (invert) result = b ? FalseOpacity : TrueOpacity;
                return result;
            }
            return FalseOpacity;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
