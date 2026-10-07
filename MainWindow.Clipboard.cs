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
/// MainWindow 的「Clipboard」部分。
/// 剪贴板与标签级命令：复制文本、状态栏文本清洗、删除标记、切换分组。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 将文本复制到系统剪贴板
    /// </summary>
    private async void CopyToClipboard(string text)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.Clipboard == null) return;

        await topLevel.Clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// 清理状态栏显示的文本：换行→空格，超长截断加省略号。
    /// </summary>
    private static string SanitizeStatusText(string text, int maxLen = 60)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sanitized = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        if (sanitized.Length <= maxLen) return sanitized;
        return sanitized[..maxLen] + "…";
    }
    
    /// <summary>
    /// 复制选中项的文本（右键菜单调用）
    /// </summary>
    private void OnCopySelectedText(object? sender, RoutedEventArgs e)
    {
        var selectedItem = Navigation.SelectedItem;
        if (selectedItem is TranslationTreeItem translationItem)
        {
            CopyToClipboard(translationItem.Text);
            StatusBar.UpdateStatus($"已复制: {SanitizeStatusText(translationItem.Text)}", StatusBarViewModel.StatusType.Info);
        }
    }
    
    /// <summary>
    /// 删除选中的标记（右键菜单调用）
    /// </summary>
    private void OnDeleteSelectedLabel(object? sender, RoutedEventArgs e)
    {
        if (!RequireEditMode()) return;
        DeleteSelectedLabel();
    }

    /// <summary>
    /// 切换选中标记的分组（右键菜单调用）
    /// </summary>
    private void OnToggleGroup(object? sender, RoutedEventArgs e)
    {
        if (!RequireEditMode()) return;

        var selectedItem = Navigation.SelectedItem;
        if (selectedItem is not TranslationTreeItem translationItem)
            return;

        if (TryGetCurrentLabels() is not { } labels)
            return;

        var labelToToggle = labels.FirstOrDefault(l => l.TextIndex == translationItem.Index);
        if (labelToToggle != null)
        {
            // 记录新旧分组索引
            int oldGroupIndex = labelToToggle.GroupIndex;
            int newGroupIndex = oldGroupIndex == 1 ? 2 : 1;
            
            // 创建并执行 ChangeGroupCommand
            var command = new ChangeGroupCommand(labelToToggle, oldGroupIndex, newGroupIndex);
            ViewModel.History.ExecuteCommand(command);
        }
    }
    
    /// <summary>
    /// 删除当前选中的标记（波纹删除：后续标记索引自动减1）
    /// </summary>
    private void DeleteSelectedLabel()
    {
        var selectedItem = Navigation.SelectedItem;
        if (selectedItem is not TranslationTreeItem translationItem)
            return;
        
        if (TryGetCurrentLabels() is not { } labels)
            return;
        
        // 找到要删除的项
        var labelToRemove = labels.FirstOrDefault(l => l.TextIndex == translationItem.Index);
        if (labelToRemove == null)
            return;
        
        // 创建并执行 DeleteLabelCommand
        var command = new DeleteLabelCommand(labels, labelToRemove);
        ViewModel.History.ExecuteCommand(command);
    }
    
    // ==================== 树视图相关方法 ====================
    
}
