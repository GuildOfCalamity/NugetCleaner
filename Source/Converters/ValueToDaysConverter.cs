using System;
using Microsoft.UI.Xaml.Data;

namespace NugetCleaner;

public class ValueToDaysConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null)
            return null;

        if (int.TryParse($"{value}", out int days))
            return $"(older than {days} {(days == 1 ? "day" : "days")})";

        return $"{value} days";
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
