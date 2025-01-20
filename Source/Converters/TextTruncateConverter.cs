using System;
using Microsoft.UI.Xaml.Data;

namespace NugetCleaner;

public class TextTruncateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null)
            return null;

        if (value is string str)
        {
            if (parameter != null && int.TryParse((string)parameter, out int amount))
                return str.Truncate(amount);
            else
                return str.Truncate(50);
        }

        return $"{value}";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
