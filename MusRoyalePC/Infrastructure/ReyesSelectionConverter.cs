using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace MusRoyalePC.Infrastructure
{
    public class ReyesSelectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSelected = value?.ToString() == parameter?.ToString();

            // Si es para el fondo (Background)
            if (targetType == typeof(Brush))
                return isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6D5B6")) : Brushes.Transparent;

            // Si es para el texto (Foreground)
            return isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2C20")) : Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
