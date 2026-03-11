using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LabelAva.Models;
using LabelAva.Services;

namespace LabelAva;

/// <summary>
/// 根据 GroupIndex 返回对应的背景色（从设置中读取，支持动态配置）
/// </summary>
public class GroupIndexToBrushConverter : IValueConverter
{
    // 静态实例用于 XAML 引用
    public static readonly GroupIndexToBrushConverter Instance = new();

    // 缓存当前颜色设置
    private ColorSettings? _cachedColorSettings;

    // 缓存的画刷
    private readonly Dictionary<int, IBrush> _brushCache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int groupIndex)
        {
            return GetBrushForGroup(groupIndex);
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取指定分组索引的画刷
    /// </summary>
    private IBrush GetBrushForGroup(int groupIndex)
    {
        // 加载最新的颜色设置
        var colorSettings = LoadColorSettings();

        // 检查缓存中是否存在该分组颜色
        if (_brushCache.TryGetValue(groupIndex, out var cachedBrush))
        {
            // 验证缓存是否仍然有效
            if (IsCacheValid(colorSettings))
            {
                return cachedBrush;
            }
        }

        // 获取颜色字符串
        if (!colorSettings.GroupColors.TryGetValue(groupIndex, out var colorHex) || string.IsNullOrEmpty(colorHex))
        {
            // 如果没有找到对应颜色，从默认设置中获取
            var defaults = ColorSettings.CreateDefaults();
            if (!defaults.GroupColors.TryGetValue(groupIndex, out colorHex))
            {
                colorHex = "#FFFFFF"; // 完全找不到时使用白色
            }
        }

        // 创建新的画刷
        var brush = CreateBrushFromHex(colorHex);

        // 更新缓存
        UpdateBrushCache(groupIndex, brush, colorSettings);

        return brush;
    }

    /// <summary>
    /// 从十六进制颜色代码创建画刷
    /// </summary>
    private static IBrush CreateBrushFromHex(string hex)
    {
        try
        {
            var color = Color.Parse(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    /// <summary>
    /// 加载颜色设置
    /// </summary>
    private ColorSettings LoadColorSettings()
    {
        try
        {
            var settings = ShortcutSettingsService.Load();
            return settings.Colors;
        }
        catch
        {
            return ColorSettings.CreateDefaults();
        }
    }

    /// <summary>
    /// 验证缓存是否有效
    /// </summary>
    private bool IsCacheValid(ColorSettings settings)
    {
        if (_cachedColorSettings == null)
            return false;

        // 简单比较：检查分组数量和颜色是否变化
        if (_cachedColorSettings.GroupColors.Count != settings.GroupColors.Count)
            return false;

        foreach (var kvp in settings.GroupColors)
        {
            if (!_cachedColorSettings.GroupColors.TryGetValue(kvp.Key, out var cachedColor))
                return false;
            if (cachedColor != kvp.Value)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 更新画刷缓存
    /// </summary>
    private void UpdateBrushCache(int groupIndex, IBrush brush, ColorSettings settings)
    {
        _cachedColorSettings = settings.Clone();
        _brushCache[groupIndex] = brush;
    }

    /// <summary>
    /// 清除缓存（当设置更改时调用）
    /// </summary>
    public static void ClearCache()
    {
        Instance._brushCache.Clear();
        Instance._cachedColorSettings = null;
    }
}
