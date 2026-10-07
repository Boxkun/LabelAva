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
/// MainWindow 的「TreeNavigation」部分。
/// 树视图的选中联动、键盘导航与居中定位。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 树视图选择变更事件（处理图片切换 & 自动折叠展开）
    /// </summary>
    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isDragActive) return;

        var selectedItem = ImageTreeView.SelectedItem;

        if (!_isSyncingSelection)
            Navigation.SelectedItem = selectedItem;

        if (selectedItem == null) return;

        ImageTreeItem? targetRootItem = null;
        if (selectedItem is TranslationTreeItem childItem)
            targetRootItem = Navigation.GetParentImageItem(childItem);
        else if (selectedItem is ImageTreeItem rootItem)
            targetRootItem = rootItem;

        if (targetRootItem != null && Navigation.TrySwitchToImage(targetRootItem.ImageName))
        { }

        if (targetRootItem != null)
            Navigation.ApplyAccordion(targetRootItem);

        if (selectedItem is TranslationTreeItem targetChildItem)
        {
            // 订阅/取消 TextChanged：当前选中项的文本变更直接推入历史栈
            if (_subscribedTranslationItem != null)
                _subscribedTranslationItem.TextChanged -= OnTranslationTextChanged;
            _subscribedTranslationItem = targetChildItem;
            _subscribedTranslationItem.TextChanged += OnTranslationTextChanged;

            CanvasControl.HighlightLabel(targetChildItem.Index);

            double currentScale = CanvasWorkspace.ZoomPercent / 100;
            double fitScale = targetRootItem?.FitScale ?? 1.0;
            if (currentScale > fitScale && !_isSelectionFromCanvas)
                CenterOnLabel(targetChildItem.Index);
            bool fromCanvas = _isSelectionFromCanvas;
            _isSelectionFromCanvas = false;

            bool autoFocus = _isKeyboardNavigation
                || !fromCanvas
                || _settingsProvider.Current.AutoFocusTextBox;
            _isKeyboardNavigation = false;

            if (Edit.IsEditMode && autoFocus && !_isUpdatingUI)
            {
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        if (_translationTextBox is not { IsEnabled: true }) return;

                        var len = _translationTextBox.Text?.Length ?? 0;
                        if (len > 0)
                        {
                            _translationTextBox.CaretIndex = 0;
                            _translationTextBox.CaretIndex = len;
                            _translationTextBox.SelectionStart = len;
                            _translationTextBox.SelectionEnd = len;
                        }
                        _translationTextBox.Focus();
                    },
                    DispatcherPriority.Loaded);
            }
        }
        else if (selectedItem is ImageTreeItem)
        {
            CanvasControl.HighlightLabel(-1);
        }
    }

    private void OnTranslationTextChanged(string oldText, string newText)
    {
        if (_isUpdatingUI || _isHandlingCtrlEnter) return;
        var labelItem = Navigation.SelectedTranslationItem?.LabelItem;
        if (labelItem == null) return;
        ViewModel.History.ExecuteCommand(new ChangeTextCommand(labelItem, oldText, newText));
    }
    
    /// <summary>
    /// 处理主窗口鼠标按键事件（用于处理鼠标侧键快捷键）
    /// </summary>
    private void OnMainWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        var updateKind = properties.PointerUpdateKind;
        
        // 只处理鼠标侧键
        if (updateKind != PointerUpdateKind.XButton1Pressed &&
            updateKind != PointerUpdateKind.XButton2Pressed)
        {
            return;
        }
        
        // 通过 ShortcutRouter 匹配鼠标侧键
        var action = _shortcutRouter.MatchPointerUpdate(updateKind);
        if (action.HasValue && Navigation.SelectedItem != null)
        {
            ExecuteShortcutAction(action.Value);
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// 树视图键盘导航处理
    /// </summary>
    private void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        var selectedItem = Navigation.SelectedItem;
        if (selectedItem == null) return;

        // 通过 ShortcutRouter 匹配可配置快捷键
        var currentGesture = new KeyGesture(e.Key, e.KeyModifiers);
        var action = _shortcutRouter.MatchKeyGesture(currentGesture);
        if (action.HasValue)
        {
            ExecuteShortcutAction(action.Value);
            e.Handled = true;
            return;
        }

        // 记录焦点变化前的根节点（用于方向键展开/收起逻辑）
        ImageTreeItem? oldRootItem = null;
        if (selectedItem is ImageTreeItem)
        {
            oldRootItem = selectedItem as ImageTreeItem;
        }
        else if (selectedItem is TranslationTreeItem currentChildItem)
        {
            oldRootItem = Navigation.GetParentImageItem(currentChildItem);
        }

        // 方向键处理：展开新焦点子项，收起旧焦点子项
        if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Right)
        {
            // 延迟执行，等待SelectionChanged事件完成
            Dispatcher.UIThread.Post(() =>
            {
                var newSelectedItem = Navigation.SelectedItem;
                if (newSelectedItem == null) return;

                ImageTreeItem? newRootItem = null;
                if (newSelectedItem is ImageTreeItem rootItem)
                {
                    newRootItem = rootItem;
                }
                else if (newSelectedItem is TranslationTreeItem newChildItem)
                {
                    newRootItem = Navigation.GetParentImageItem(newChildItem);
                }

                // 收起旧的焦点项（如果与新的不同）
                if (oldRootItem != null && newRootItem != null && oldRootItem != newRootItem)
                {
                    oldRootItem.IsExpanded = false;
                }

                // 展开新的焦点项
                if (newRootItem != null)
                {
                    newRootItem.IsExpanded = true;
                    Navigation.LastFocusedRootItem = newRootItem;
                }
                
                // 确保新选中的项获得焦点，触发视图滚动
                var container = ImageTreeView.ContainerFromItem(newSelectedItem) as Control;
                container?.Focus();
            }, DispatcherPriority.Background);
        }
    }
    
    /// <summary>
    /// 将视野中心对准指定编号的标注（委托给 CanvasWorkspaceViewModel）
    /// </summary>
    private void CenterOnLabel(int labelIndex)
    {
        if (CanvasControl.CurrentImage == null || TryGetCurrentLabels() is not { } labels)
            return;
        
        // 找到对应编号的标注
        LabelItem? targetLabel = null;
        foreach (var label in labels)
        {
            if (label.TextIndex == labelIndex)
            {
                targetLabel = label;
                break;
            }
        }
        
        if (targetLabel == null) return;
        
        // 委托给 CanvasWorkspace（传入归一化坐标）
        CanvasWorkspace.CenterOnLabel(targetLabel.X, targetLabel.Y);
    }

    // ==================== TreeViewItem 拖拽事件处理（纯 Pointer 事件 + 指针捕获）====================
    // 状态机：IDLE → PENDING → DRAGGING → IDLE
    //   PENDING: PointerPressed 后，等待超过防抖阈值
    //   DRAGGING: 超过阈值后捕获指针，中点判定落点 + 浮动预览 + 落点横线

}
