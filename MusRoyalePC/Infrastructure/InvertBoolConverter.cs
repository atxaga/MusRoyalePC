using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusRoyalePC
{
    /// <summary>
    /// Invierte un bool. Opcionalmente puede devolver Visibility si ConverterParameter == "Visibility".
    /// </summary>
    public sealed class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool bb && bb;
            bool inverted = !b;

            if (parameter is string s && s.Equals("Visibility", StringComparison.OrdinalIgnoreCase))
                return inverted ? Visibility.Visible : Visibility.Collapsed;

            return inverted;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v != Visibility.Visible;

            return value is bool b ? !b : Binding.DoNothing;
        }
    }
}
