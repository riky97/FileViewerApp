using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;

namespace FileViewerApp.Converters
{
    public class BooleanToThicknessConverter : IValueConverter
    {
        // Converts: true => Thickness 0 (no border); false => Thickness 1 (visible border)
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            // When valid (true) we want no border: 0. When invalid (false) we want border: 1
            return flag ? new Thickness(0) : new Thickness(1);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Thickness th)
            {
                return th.Left == 0; // 0 -> true(valid); 1 -> false(invalid)
            }
            return true;
        }
    }
}
