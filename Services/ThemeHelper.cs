using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;

namespace LabelAva.Services;

internal static class ThemeHelper
{
    internal static IBrush? GetBrush(string key)
    {
        if (Application.Current == null) return null;

        // 【修改点】：直接使用 Application.Current.TryGetResource，而不是 .Resources.TryGetResource
        if (!Application.Current.TryGetResource(
                key, 
                Application.Current.ActualThemeVariant, 
                out var value) || value is null)
        {
            // 作为备选方案，也可以使用 TryFindResource，它会自动向上查找资源树
            // if (!Application.Current.TryFindResource(key, out value) || value is null)
            return null;
        }

        if (value is IBrush brush)
            return brush;

        if (value is Color color)
            return new SolidColorBrush(color);

        return null;
    }
}