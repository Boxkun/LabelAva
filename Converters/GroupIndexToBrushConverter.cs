using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LabelAva.Models;
using LabelAva.Services;

namespace LabelAva;

public class GroupIndexToBrushConverter : IValueConverter
{
    public static readonly GroupIndexToBrushConverter Instance = new();

    private AppSettingsProvider? _provider;
    private ColorSettings? _cachedColorSettings;
    private readonly Dictionary<int, IBrush> _brushCache = new();

    public static void Initialize(AppSettingsProvider provider)
    {
        Instance._provider = provider;
    }

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

    private IBrush GetBrushForGroup(int groupIndex)
    {
        var colorSettings = _provider?.Current.Colors ?? ColorSettings.CreateDefaults();

        if (_brushCache.TryGetValue(groupIndex, out var cachedBrush))
        {
            if (IsCacheValid(colorSettings))
            {
                return cachedBrush;
            }
        }

        if (!colorSettings.GroupColors.TryGetValue(groupIndex, out var colorHex) || string.IsNullOrEmpty(colorHex))
        {
            var defaults = ColorSettings.CreateDefaults();
            if (!defaults.GroupColors.TryGetValue(groupIndex, out colorHex))
            {
                colorHex = "#FFFFFF";
            }
        }

        var brush = CreateBrushFromHex(colorHex);

        UpdateBrushCache(groupIndex, brush, colorSettings);

        return brush;
    }

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

    private bool IsCacheValid(ColorSettings settings)
    {
        if (_cachedColorSettings == null)
            return false;

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

    private void UpdateBrushCache(int groupIndex, IBrush brush, ColorSettings settings)
    {
        _cachedColorSettings = settings.Clone();
        _brushCache[groupIndex] = brush;
    }

    public static void InvalidateCache()
    {
        Instance._brushCache.Clear();
        Instance._cachedColorSettings = null;
    }
}
