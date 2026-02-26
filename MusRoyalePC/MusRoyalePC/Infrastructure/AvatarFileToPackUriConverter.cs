using System;
using System.Globalization;
using System.Windows.Data;

namespace MusRoyalePC.Infrastructure;

public sealed class AvatarFileToPackUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var file = value as string;
        if (string.IsNullOrWhiteSpace(file))
            file = "avadef.png";

        return new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
