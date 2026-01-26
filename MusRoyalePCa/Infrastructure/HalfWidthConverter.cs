using System;
using System.Globalization;
using System.Windows.Data;

namespace MusRoyalePC
{
    public sealed class HalfWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0.5; // default to half
            if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
            {
                percent = Math.Clamp(p, 0.0, 1.0);
            }

            if (value is double d && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                return Math.Max(0, d * percent);
            }
            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
