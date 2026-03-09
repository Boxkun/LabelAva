using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LabelAva;

/// <summary>
/// 根据 GroupIndex 返回对应的背景色
/// </summary>
public class GroupIndexToClassConverter : IValueConverter
{
    // 静态实例用于 XAML 引用
    public static readonly GroupIndexToClassConverter Instance = new();
    
    // 预设颜色
    private static readonly IBrush Group1Brush = new SolidColorBrush(Color.Parse("#FFE6E6")); // 红色
    private static readonly IBrush Group2Brush = new SolidColorBrush(Color.Parse("#E6E6FF")); // 蓝色
    private static readonly IBrush DefaultBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int groupIndex)
        {
            return groupIndex switch
            {
                1 => Group1Brush,  // 红色
                2 => Group2Brush,  // 蓝色
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
