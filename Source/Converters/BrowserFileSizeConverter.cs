using System;
using Microsoft.UI.Xaml.Data;

namespace NugetCleaner;

public class BrowserFileSizeConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null)
            return null;

        if (value is long lng)
            return lng.HumanReadableSize();

        return $"{value}";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
