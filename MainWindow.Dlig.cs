using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Layout;
using LabelAva.Services;
using LabelAva.Models;
using LabelAva.Views;
using LabelAva.Commands;
using System.Linq;
using LabelAva.ViewModels;
using System.Diagnostics;
using Avalonia.Input.Platform;


namespace LabelAva;

/// <summary>
/// MainWindow 的「Dlig」部分。
/// 连字（dlig）配置的应用：字体家族/OpenType 特性、快捷输入槽装载、按钮强调色。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 应用连字配置：设置编辑器字体、OpenType 特性、快捷输入按钮、树状图译文字体
    /// </summary>
    private void ApplyDligConfig()
    {
        if (_translationTextBox == null)
            return;

        // 始终加载默认快捷输入
        Edit.QuickInputSlots.Clear();
        foreach (var slot in _settingsProvider.Current.DefaultQuickInputs)
            Edit.QuickInputSlots.Add(slot);

        var configName = _settingsProvider.Current.ActiveDligConfig;

        if (string.IsNullOrWhiteSpace(configName))
        {
            Edit.ActiveDligFontFamily = null;
            Edit.ActiveDligFontFeatures = null;
            return;
        }

        var config = DligConfigService.LoadConfig(configName);
        if (config == null)
        {
            Edit.ActiveDligFontFamily = null;
            Edit.ActiveDligFontFeatures = null;
            StatusBar.UpdateStatus(
                $"连字配置 '{configName}' 加载失败，已回退到默认",
                StatusBarViewModel.StatusType.Warn);
            return;
        }

        // 追加载连字配置的快捷输入按钮
        if (config.QuickInputs != null)
        {
            foreach (var slot in config.QuickInputs)
            {
                slot.IsFromDligConfig = true;
                Edit.QuickInputSlots.Add(slot);
            }
        }

        // 应用字体和 OpenType 特性
        if (string.IsNullOrWhiteSpace(config.FontFamily))
        {
            Edit.ActiveDligFontFamily = null;
            Edit.ActiveDligFontFeatures = null;
            return;
        }

        var fontFamily = new FontFamily(config.FontFamily);
        var typeface = new Typeface(fontFamily);
        var fontInstalled = FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface)
            && string.Equals(glyphTypeface.FamilyName, config.FontFamily, StringComparison.OrdinalIgnoreCase);

        if (!fontInstalled)
        {
            Edit.ActiveDligFontFamily = null;
            Edit.ActiveDligFontFeatures = null;
            StatusBar.UpdateStatus(
                $"字体 '{config.FontFamily}' 未安装，连字功能不可用",
                StatusBarViewModel.StatusType.Warn);
            return;
        }

        FontFeatureCollection? features = null;
        if (!string.IsNullOrWhiteSpace(config.FontFeatures))
        {
            features = new FontFeatureCollection();
            foreach (var part in config.FontFeatures.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                features.Add(FontFeature.Parse(part));
        }

        Edit.ActiveDligFontFamily = fontFamily;
        Edit.ActiveDligFontFeatures = features;
        Resources["DligFontFamily"] = fontFamily;
        Resources["DligFontFeatures"] = features;
    }

    private void ApplyDligButtonAccentBackground()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var accentBrush = ThemeHelper.GetBrush("SystemControlHighlightListAccentLowBrush");
            if (accentBrush == null) return;
            var count = 0;
            foreach (var btn in QuickInputItemsControl.GetVisualDescendants().OfType<Button>())
            {
                if (btn.DataContext is QuickInputSlot slot && slot.IsFromDligConfig)
                {
                    btn.Background = accentBrush;
                    count++;
                }
            }
            System.Diagnostics.Debug.WriteLine($"[DligAccent] Applied to {count} buttons, total buttons: {QuickInputItemsControl.GetVisualDescendants().OfType<Button>().Count()}");
        }, DispatcherPriority.Background);
    }

}
