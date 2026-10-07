using System;
using System.Collections.Generic;
using Avalonia.Media;
using LabelAva.Models;

namespace LabelAva.Services;

/// <summary>连字配置的解析结果：要写入 ViewModel 的字体/特性、快捷输入槽，以及可选警告。</summary>
public sealed class DligApplicationResult
{
    /// <summary>默认快捷输入 + 连字配置自带快捷输入（顺序与原实现一致）。</summary>
    public List<QuickInputSlot> Slots { get; init; } = new();

    /// <summary>要写入 EditViewModel 的字体家族；null 表示不启用连字字体。</summary>
    public FontFamily? FontFamily { get; init; }

    /// <summary>要写入 EditViewModel 的 OpenType 特性；null 表示不设置。</summary>
    public FontFeatureCollection? Features { get; init; }

    /// <summary>
    /// 是否同时写入窗口资源（<c>Resources["DligFontFamily"]</c> / <c>["DligFontFeatures"]</c>）。
    ///
    /// 原实现只在「完全解析成功」时写这两个键；未配置 / 配置加载失败 / 字体未安装三条分支
    /// 刻意保持不动（树状图会沿用上一次的字体）。这里如实保留该行为，
    /// 所以这个标志不是多余的状态，而是在记录一个既有的语义。
    /// </summary>
    public bool WritesWindowResources { get; init; }

    /// <summary>需要提示用户的警告文案；null 表示无需提示。</summary>
    public string? Warning { get; init; }
}

/// <summary>
/// 连字（dlig）配置的解析：把「当前设置」变成「字体 + OpenType 特性 + 快捷输入槽 + 可选警告」。
///
/// 抽出来的理由（对应 refactor.md #7）：原实现把「读配置、校验字体是否安装、算特性、
/// 填充 ViewModel 集合、写窗口资源、报状态栏」全揉在一个视图方法里 —— 既无法单测，
/// 也让 ViewModel 的填充职责留在了 View 层。
///
/// 这里只做配置读取与纯计算，不碰任何 UI 控件与 ViewModel，因此可以直接单测。
/// </summary>
public static class DligConfigResolver
{
    public static DligApplicationResult Resolve(AppSettings settings)
    {
        var slots = new List<QuickInputSlot>(settings.DefaultQuickInputs);

        var configName = settings.ActiveDligConfig;
        if (string.IsNullOrWhiteSpace(configName))
            return new DligApplicationResult { Slots = slots };

        var config = DligConfigService.LoadConfig(configName);
        if (config is null)
        {
            return new DligApplicationResult
            {
                Slots = slots,
                Warning = $"连字配置 '{configName}' 加载失败，已回退到默认",
            };
        }

        // 追加载连字配置的快捷输入按钮
        if (config.QuickInputs != null)
        {
            foreach (var slot in config.QuickInputs)
            {
                slot.IsFromDligConfig = true;
                slots.Add(slot);
            }
        }

        // 没有指定字体家族：只装载快捷输入，不启用字体
        if (string.IsNullOrWhiteSpace(config.FontFamily))
            return new DligApplicationResult { Slots = slots };

        var fontFamily = new FontFamily(config.FontFamily);
        var typeface = new Typeface(fontFamily);
        var fontInstalled = FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface)
            && string.Equals(glyphTypeface.FamilyName, config.FontFamily, StringComparison.OrdinalIgnoreCase);

        if (!fontInstalled)
        {
            return new DligApplicationResult
            {
                Slots = slots,
                Warning = $"字体 '{config.FontFamily}' 未安装，连字功能不可用",
            };
        }

        FontFeatureCollection? features = null;
        if (!string.IsNullOrWhiteSpace(config.FontFeatures))
        {
            features = new FontFeatureCollection();
            foreach (var part in config.FontFeatures.Split(
                         ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                features.Add(FontFeature.Parse(part));
            }
        }

        return new DligApplicationResult
        {
            Slots = slots,
            FontFamily = fontFamily,
            Features = features,
            WritesWindowResources = true,
        };
    }
}
