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
/// MainWindow 的「Documents」部分。
/// 文档与导航事件的响应，以及菜单命令处理器（打开/新建/退出/首选项/关于/图片关联管理器）。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// DocumentViewModel.DocumentOpened 事件处理
    /// </summary>
    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        Navigation.InitializeNavigation(e.ImageFolderPath, e.ImageNames, e.ImagePathMapping);
        PageNavGrid.IsVisible = true;

        if (Navigation.ImageNames.Count > 0)
        {
            Navigation.BuildTreeView(Document.TranslationData);
            ShowMainContent();
            _ = SetFocusAfterDelayAsync();
        }
    }

    /// <summary>
    /// DocumentViewModel.DocumentClosed 事件处理
    /// </summary>
    private void OnDocumentClosed(object? sender, EventArgs e)
    {
        // 清理图片和标注（ClearCanvas 内部会 Dispose 图片并重置 _isFirstImageLoaded）
        CanvasControl.ClearCanvas();
        
        // 重置视口
        CanvasWorkspace.ResetTransform();
        CanvasWorkspace.UpdateImageSize(new Size(0, 0));

        // 清理导航状态（TranslationData 由 DocumentViewModel 管理）
        Navigation.ClearNavigation();
        PageNavGrid.IsVisible = false;

        ShowWelcomeScreen();
    }
    
    /// <summary>
    /// NavigationViewModel.CurrentImageChanged 事件处理
    /// </summary>
    private void OnNavigationCurrentImageChanged(object? sender, EventArgs e)
    {
        LoadCurrentImage();
        CalculateFitTransform();
        UpdateLabels();
    }
    
    /// <summary>
    /// NavigationViewModel.SelectedItemChanged 事件处理（由 VM 侧发起的选中项变更）
    /// </summary>
    private void OnNavigationSelectedItemChanged(object? sender, EventArgs e)
    {
        if (_isSyncingSelection) return;
        if (Navigation.SelectedItem != null && ImageTreeView.SelectedItem != Navigation.SelectedItem)
        {
            _isSyncingSelection = true;
            try
            {
                ImageTreeView.SelectedItem = Navigation.SelectedItem;
            }
            finally
            {
                _isSyncingSelection = false;
            }

            // 将选中项滚入视野（延迟到布局完成后）
            Dispatcher.UIThread.Post(() =>
            {
                ImageTreeView.ScrollIntoView(Navigation.SelectedItem);
            }, DispatcherPriority.Loaded);
        }
    }
    
    private void OnCanvasTransformChanged(object? sender, EventArgs e)
    {
        // 同步矩阵到 UI 控件
        CanvasControl.ApplyTransform();
        // 同步缩放百分比到状态栏
        StatusBar.UpdateZoom(CanvasWorkspace.ZoomPercent);
        // 同步 FitScale 到树视图项
        SaveCurrentFitScale(CanvasWorkspace.CurrentFitScale);
    }
    
    /// <summary>
    /// 延迟设置焦点到树状视图，确保菜单已完全关闭
    /// </summary>
    private async Task SetFocusAfterDelayAsync()
    {
        // 延迟一段时间以确保菜单关闭完成
        await Task.Delay(100);
        
        FocusFirstTreeViewItem();
    }
    
    /// <summary>
    /// 聚焦到第一个 TreeViewItem
    /// </summary>
    private void FocusFirstTreeViewItem()
    {
        if (Navigation.TreeItems.Count == 0) return;

        // 展开第一个项（如果需要）
        Navigation.TreeItems[0].IsExpanded = true;

        // 选中第一个项
        ImageTreeView.SelectedItem = Navigation.TreeItems[0];

        // 等待布局，再获取容器并设置焦点
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var container = ImageTreeView.ContainerFromItem(Navigation.TreeItems[0]);
            if (container != null)
            {
                (container as Control)?.Focus();
            }
            else
            {
                // 如果容器未准备好，退回到清除焦点
                TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
            }
        }, DispatcherPriority.Background);
    }
    
    // ==================== 菜单事件处理 ====================
    
    /// <summary>
    /// 刷新树状视图（重新绑定数据以应用新颜色）
    /// </summary>
    private void RefreshTreeView()
    {
        // 触发 TreeView 重新渲染 - 简单方式是重新设置 ItemsSource
        var currentSelectedItem = Navigation.SelectedItem;

        // 重新设置 ItemsSource 以触发刷新
        ImageTreeView.ItemsSource = null;
        ImageTreeView.ItemsSource = Navigation.TreeItems;

        // 恢复选中状态
        if (currentSelectedItem != null)
        {
            Navigation.SelectedItem = currentSelectedItem;
        }
    }
    
    /// <summary>
    /// 历史状态变化事件处理（由 HistoryViewModel.HistoryStateChanged 触发）
    /// </summary>
    private void OnHistoryStateChanged(object? sender, EventArgs e)
    {
        // 【防卫】初始化未完成前不处理
        if (!_isInitialized)
            return;

        // 仅在文档打开时设置脏标记（避免关闭文档时 _history.Clear() 触发 SetDirty 覆盖 IsDirty=false）
        if (Document.HasDocument)
        {
            Document.SetDirty(true);
            // 崩溃恢复：打断 TextChanged 防抖，立即落盘
            _recoveryDebounce?.Stop();
            Document.WriteImmediateRecovery();
        }

        // 【核心修复】将视图重建推迟到更晚执行，确保 SelectionChanged 等事件完全处理完毕
        // 使用更低的优先级，确保在 TextBox 值设置和光标定位之后执行
        Dispatcher.UIThread.Post(() =>
        {
            _isUpdatingUI = true; // 上锁
            try
            {
                RebuildCurrentView(); // 重新生成TreeView和Canvas标签
            }
            finally
            {
                _isUpdatingUI = false; // 解锁
            }
        }, DispatcherPriority.Loaded); // 使用 Loaded 优先级，比 Input 优先级更低
    }

    /// <summary>
    /// 处理来自 WelcomeView 的打开翻译请求
    /// </summary>
    private async void OnOpenTranslationRequested(object? sender, RoutedEventArgs e)
    {
        await Document.OpenCommand.ExecuteAsync(null);
    }
    
    /// <summary>
    /// 处理来自 WelcomeView 的新建翻译请求
    /// </summary>
    private async void OnNewTranslationRequested(object? sender, RoutedEventArgs e)
    {
        await Document.NewCommand.ExecuteAsync(null);
    }
    
    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnImageAssociationManager(object? sender, RoutedEventArgs e)
    {
        if (!Document.HasDocument) return;

        var result = await Document.ShowImageAssociationManagerAsync();
        if (result == null) return;

        Document.ApplyAssociationResult(result);

        // 同步 Navigation 的 ImagePathMapping
        Navigation.ImagePathMapping = new Dictionary<string, string>(Document.ImagePathMapping);
        if (!string.IsNullOrEmpty(Document.ImageFolderPath))
        {
            Navigation.ImageFolderPath = Document.ImageFolderPath;
        }

        // 刷新 UI
        Navigation.BuildTreeView(Document.TranslationData);
        LoadCurrentImage();
        CalculateFitTransform();
        UpdateLabels();
    }
    
    private async void OnPreferences(object? sender, RoutedEventArgs e)
    {
        var preferencesWindow = new Views.PreferencesWindow(_settingsProvider);
        await preferencesWindow.ShowDialog(this);
    }

    private async void OnAbout(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new Views.AboutWindow();
        await aboutWindow.ShowDialog(this);
    }
    
    // ==================== ImageContainer 事件处理 ====================
    
}
