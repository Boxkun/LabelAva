using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LabelAva.Services;
using LabelAva.Models;
using LabelAva.ViewModels;


namespace LabelAva;

/// <summary>
/// MainWindow 的「Dlig」部分。
/// 只负责把 <see cref="DligConfigResolver"/> 的结果推给 ViewModel 与窗口资源，
/// 以及给连字快捷输入按钮加强调色 —— 策略本身在解析器里，视图层不再持有它。
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

        var result = DligConfigResolver.Resolve(_settingsProvider.Current);

        Edit.QuickInputSlots.Clear();
        foreach (var slot in result.Slots)
            Edit.QuickInputSlots.Add(slot);

        Edit.ActiveDligFontFamily = result.FontFamily;
        Edit.ActiveDligFontFeatures = result.Features;

        // 解析器只在完全成功时要求写窗口资源，失败分支刻意保留上一次的值
        if (result.WritesWindowResources)
        {
            Resources["DligFontFamily"] = result.FontFamily;
            Resources["DligFontFeatures"] = result.Features;
        }

        if (result.Warning != null)
            StatusBar.UpdateStatus(result.Warning, StatusBarViewModel.StatusType.Warn);
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
