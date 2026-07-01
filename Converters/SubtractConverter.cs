using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LabelAva;

/// <summary>从 double 值减去指定量，用于动态 MaxWidth 计算。</summary>
public class SubtractConverter : IValueConverter
{
    public static readonly SubtractConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            var subtrahend = parameter switch
            {
                double p => p,
                string s when double.TryParse(s, out var p) => p,
                _ => 0.0
            };
            return Math.Max(0, d - subtrahend);
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
