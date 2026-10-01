using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LabelAva.Models;

namespace LabelAva.Tests;

/// <summary>
/// 场景与测试共用的 UI 驱动小工具。
/// 刻意走真实控件与真实 RoutedEvent（而不是去反射调私有处理器），
/// 这样 MainWindow 那层视图胶水才会真正被覆盖。
/// </summary>
internal static class UiDriver
{
    /// <summary>快捷输入工具栏里是否已经实例化出来自连字配置的按钮（纯查询，不泵调度器）。</summary>
    public static bool HasDligQuickInputButton(Window window)
    {
        var items = window.FindControl<ItemsControl>("QuickInputItemsControl");
        return items is not null
               && items.GetVisualDescendants().OfType<Button>()
                   .Any(b => b.DataContext is QuickInputSlot { IsFromDligConfig: true });
    }

    /// <summary>找到第一个来自连字配置的快捷输入按钮。</summary>
    public static Button FindDligQuickInputButton(Window window)
    {
        var items = window.FindControl<ItemsControl>("QuickInputItemsControl")
            ?? throw new InvalidOperationException("找不到 QuickInputItemsControl");

        var buttons = items.GetVisualDescendants().OfType<Button>().ToList();
        return buttons.FirstOrDefault(b => b.DataContext is QuickInputSlot { IsFromDligConfig: true })
            ?? throw new InvalidOperationException(
                $"没有找到来自连字配置的快捷输入按钮（当前共 {buttons.Count} 个 Button）");
    }

    /// <summary>点击快捷输入按钮（触发真实 Click 路由事件）。</summary>
    public static void Click(Button button)
        => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// 全选文本框内容。
    ///
    /// 两个坑必须按这个顺序写：
    /// 1) **先设 CaretIndex，再设 SelectionStart/SelectionEnd** —— Avalonia 里设置 CaretIndex
    ///    会把已有选区收拢掉，反过来写等于自己把选区抹了（AGENTS.md 里那条「光标移到末尾」
    ///    的序列 CaretIndex → SelectionStart/End 也正是这个原因）。
    /// 2) 调用后**不要**再泵调度器 —— 视图重建链里 POST 的光标修复任务同样会把选区收拢到末尾，
    ///    那样就退化成「插入」而不是「替换」了。
    /// </summary>
    public static void SelectAllText(TextBox textBox)
    {
        textBox.CaretIndex = 0;
        textBox.SelectionStart = 0;
        textBox.SelectionEnd = textBox.Text?.Length ?? 0;
    }

    /// <summary>把光标放到指定位置且不留下选区（同样先设 CaretIndex）。</summary>
    public static void CollapseCaretAt(TextBox textBox, int position)
    {
        textBox.CaretIndex = position;
        textBox.SelectionStart = position;
        textBox.SelectionEnd = position;
    }
}
