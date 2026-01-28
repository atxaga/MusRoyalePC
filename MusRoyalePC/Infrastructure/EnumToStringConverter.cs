using System;
using System.Globalization;
using System.Windows.Data;

namespace MusRoyalePC
{
    public sealed class EnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, s, ignoreCase: true);
                }
                catch
                {
                    // ignored
                }
            }
            return Binding.DoNothing;
        }
    }
}
