using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using LabelAva.Models;
using LabelAva.Services;

namespace LabelAva.Tests;

/// <summary>
/// 连字配置解析的分支测试。
///
/// 这些分支（未配置 / 字体名为空 / 字体未安装 / 配置加载失败 / 解析成功）
/// 原本全揉在 MainWindow.ApplyDligConfig 里、且直接写 UI 资源，无法单测；
/// 抽成纯函数后可以逐条断言「装载了哪些槽、是否启用字体、给什么警告」。
/// </summary>
public class DligConfigResolverTests
{
    [AvaloniaFact]
    public void 未配置连字时只装载默认快捷输入且不写窗口资源()
    {
        using var appData = new TempAppData();
        var settings = AppSettings.CreateDefaults();
        settings.ActiveDligConfig = null;

        var result = DligConfigResolver.Resolve(settings);

        Assert.Equal(settings.DefaultQuickInputs.Count, result.Slots.Count);
        Assert.All(result.Slots, slot => Assert.False(slot.IsFromDligConfig));
        Assert.Null(result.FontFamily);
        Assert.Null(result.Features);
        Assert.False(result.WritesWindowResources);
        Assert.Null(result.Warning);
    }

    [AvaloniaFact]
    public void 字体家族为空时装载连字快捷输入但不启用字体()
    {
        using var appData = new TempAppData();
        DligConfigService.SaveConfig("EmptyFont", new DligFontConfig
        {
            FontFamily = "",
            FontFeatures = "dlig=1",
            QuickInputs = new List<QuickInputSlot>
            {
                new() { Label = "#02", Character = "#02" },
                new() { Label = "@01", Character = "@01" },
            },
        });

        var settings = AppSettings.CreateDefaults();
        settings.ActiveDligConfig = "EmptyFont";

        var result = DligConfigResolver.Resolve(settings);

        Assert.Equal(settings.DefaultQuickInputs.Count + 2, result.Slots.Count);
        Assert.Equal(2, result.Slots.Count(slot => slot.IsFromDligConfig));
        Assert.Null(result.FontFamily);
        Assert.False(result.WritesWindowResources);
        Assert.Null(result.Warning);
    }

    [AvaloniaFact]
    public void 字体未安装时给出警告且不启用字体()
    {
        using var appData = new TempAppData();
        DligConfigService.SaveConfig("MissingFont", new DligFontConfig
        {
            FontFamily = "LabelAva 不存在的字体 12345",
            FontFeatures = "dlig=1",
            QuickInputs = new List<QuickInputSlot> { new() { Label = "~~", Character = "~~" } },
        });

        var settings = AppSettings.CreateDefaults();
        settings.ActiveDligConfig = "MissingFont";

        var result = DligConfigResolver.Resolve(settings);

        Assert.Null(result.FontFamily);
        Assert.False(result.WritesWindowResources);
        Assert.NotNull(result.Warning);
        Assert.Contains("未安装", result.Warning);
        Assert.Equal(1, result.Slots.Count(slot => slot.IsFromDligConfig));
    }

    [AvaloniaFact]
    public void 配置加载失败时给出警告()
    {
        using var appData = new TempAppData();
        var settings = AppSettings.CreateDefaults();
        settings.ActiveDligConfig = "根本不存在的配置名";

        var result = DligConfigResolver.Resolve(settings);

        Assert.Null(result.FontFamily);
        Assert.False(result.WritesWindowResources);
        Assert.NotNull(result.Warning);
        Assert.Contains("加载失败", result.Warning);
        Assert.Equal(settings.DefaultQuickInputs.Count, result.Slots.Count);
    }

    [AvaloniaFact]
    public void 字体已安装时解析出字体与OpenType特性并写窗口资源()
    {
        using var appData = new TempAppData();

        var family = FindInstalledFamilyName();
        if (family is null)
            Assert.Skip($"本机找不到能被 Avalonia 按家族名正常解析的字体，跳过。诊断: {ProbeFontResolution()}");

        DligConfigService.SaveConfig("InstalledFont", new DligFontConfig
        {
            FontFamily = family!,
            FontFeatures = "dlig=1",
        });

        var settings = AppSettings.CreateDefaults();
        settings.ActiveDligConfig = "InstalledFont";

        var result = DligConfigResolver.Resolve(settings);

        Assert.NotNull(result.FontFamily);
        Assert.Equal(family, result.FontFamily!.Name);
        Assert.NotNull(result.Features);
        Assert.Contains(result.Features!, f => f.Tag.ToString() == "dlig" && f.Value == 1);
        Assert.True(result.WritesWindowResources);
        Assert.Null(result.Warning);
    }

    /// <summary>找一个 Avalonia 能按名字解析、且解析结果的 FamilyName 与之相等的字体家族。</summary>
    private static string? FindInstalledFamilyName()
    {
        foreach (var candidate in new[]
                 {
                     "Arial", "Segoe UI", "Tahoma", "Consolas", "Helvetica", "DejaVu Sans", "Noto Sans"
                 })
        {
            var fontFamily = new FontFamily(candidate);
            if (FontManager.Current.TryGetGlyphTypeface(new Typeface(fontFamily), out var glyphTypeface)
                && glyphTypeface is not null
                && string.Equals(glyphTypeface.FamilyName, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>诊断用：报告 Avalonia 当前环境能否按家族名解析系统字体。</summary>
    private static string ProbeFontResolution()
    {
        var ok = FontManager.Current.TryGetGlyphTypeface(
            new Typeface(new FontFamily("Arial")), out var glyphTypeface);
        var familyName = ok && glyphTypeface is not null ? glyphTypeface.FamilyName : "<解析失败>";
        return $"Arial: TryGetGlyphTypeface={ok}, FamilyName='{familyName}', " +
               $"FontManager={FontManager.Current.GetType().Name}";
    }
}
