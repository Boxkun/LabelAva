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
/// MainWindow 的「TreeDragDrop」部分。
/// 树视图拖拽重排的状态机：按下/移动/释放、放置目标计算、拖拽预览与插入线、重排提交。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    private void OnTreeViewItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Control);
        if (point.Properties.IsLeftButtonPressed && sender is Control control)
        {
            var dataContext = control.DataContext;

            // 仅允许拖拽 TranslationTreeItem (子节点)
            if (dataContext is TranslationTreeItem treeItem)
            {
                _treeDragStartPoint = point.Position;
                _isTreeItemDragging = true;
                _draggedTreeItem = treeItem;
                control.PointerCaptureLost += OnTreeDragPointerCaptureLost;
            }
        }
    }

    private void OnTreeViewItemPointerMoved(object? sender, PointerEventArgs e)
    {
        // 阶段1：PENDING — 阈值检测，决定是否进入拖拽
        if (_isTreeItemDragging && !_isDragActive && _draggedTreeItem != null)
        {
            var point = e.GetCurrentPoint(sender as Control);
            var diff = point.Position - _treeDragStartPoint;

            if (Math.Abs(diff.X) > TreeDragThreshold || Math.Abs(diff.Y) > TreeDragThreshold)
            {
                if (!RequireEditMode())
                {
                    _isTreeItemDragging = false;
                    _draggedTreeItem = null;
                    return;
                }
                // 进入 DRAGGING 状态
                _isTreeItemDragging = false;
                _isDragActive = true;
                e.Pointer.Capture((Control)sender!);
                ShowTreePreview(_draggedTreeItem);
                UpdateTreePreviewPos(e);
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
            return;
        }

        // 阶段2：DRAGGING — 中点判定落点 + 预览位置 + 落点横线
        if (_isDragActive && _draggedTreeItem != null && _imageTreeView != null)
        {
            var treeViewPos = e.GetPosition(_imageTreeView);
            var newTarget = GetTreeDropTarget(treeViewPos);

            if (newTarget != _currentDropTarget)
            {
                _currentDropTarget = newTarget;
                UpdateTreeDropLine(newTarget);
            }

            UpdateTreePreviewPos(e);
        }
    }

    private void OnTreeViewItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragActive && _draggedTreeItem != null)
        {
            PerformReorder(_draggedTreeItem, _currentDropTarget);
        }

        // 清理状态前，若仍在捕获则释放
        if (_isDragActive)
        {
            e.Pointer.Capture(null);
        }
        CleanupTreeDragState();
    }

    /// <summary>
    /// 在 TreeView 中通过中点判定查找有效的放置目标。
    /// 仅允许同图片下的 TranslationTreeItem 作为目标。
    /// 返回 null 表示插入到列表末尾。
    /// </summary>
    private TranslationTreeItem? GetTreeDropTarget(Point posInTreeView)
    {
        if (_imageTreeView == null || _draggedTreeItem == null) return null;

        var sourceParent = Navigation.GetParentImageItem(_draggedTreeItem);
        if (sourceParent == null) return null;

        // 收集所有可见 TranslationTreeItem 容器，筛选同父节点、排序后找落点
        var candidates = new List<(double MidY, TranslationTreeItem Item)>();

        foreach (var container in _imageTreeView.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (container.DataContext is not TranslationTreeItem treeItem) continue;
            if (treeItem == _draggedTreeItem) continue;
            if (Navigation.GetParentImageItem(treeItem) != sourceParent) continue;

            var bounds = container.TranslatePoint(new Point(0, 0), _imageTreeView);
            if (!bounds.HasValue) continue;

            double midY = bounds.Value.Y + container.Bounds.Height / 2;
            candidates.Add((midY, treeItem));
        }

        // 按 Y 坐标排序
        candidates.Sort((a, b) => a.MidY.CompareTo(b.MidY));

        // 找到首个 midY 在光标下方的项 — 即"在此项前插入"
        foreach (var (midY, treeItem) in candidates)
        {
            if (posInTreeView.Y < midY)
                return treeItem;
        }

        // 光标低于所有候选项 → 末尾插入
        return null;
    }

    private TreeViewItem? FindContainerForItem(TranslationTreeItem item)
    {
        if (_imageTreeView == null) return null;
        return _imageTreeView.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .FirstOrDefault(c => c.DataContext == item);
    }

    private void ShowTreePreview(TranslationTreeItem item)
    {
        TreeDragPreviewText.Text = item.Text;
        TreeDragPreview.IsVisible = true;
    }

    private void UpdateTreePreviewPos(PointerEventArgs e)
    {
        var canvasPos = e.GetPosition(TreeDragPreviewCanvas);
        Canvas.SetLeft(TreeDragPreview, canvasPos.X);
        Canvas.SetTop(TreeDragPreview, canvasPos.Y);
    }

    private void UpdateTreeDropLine(TranslationTreeItem? target)
    {
        if (_imageTreeView == null) return;

        if (target != null)
        {
            var container = FindContainerForItem(target);
            if (container != null)
            {
                var bounds = container.TranslatePoint(new Point(0, 0), TreeDropIndicatorCanvas);
                if (bounds.HasValue)
                {
                    Canvas.SetLeft(TreeDropLine, bounds.Value.X);
                    Canvas.SetTop(TreeDropLine, bounds.Value.Y);
                    TreeDropLine.Width = container.Bounds.Width;
                    TreeDropLine.IsVisible = true;
                    return;
                }
            }
        }
        else 
        {
            // 末尾插入：在最后一个同父节点的 TranslationTreeItem 容器底部画线
            var lastContainer = FindLastVisibleTranslationContainer();
            if (lastContainer != null)
            {
                var bounds = lastContainer.TranslatePoint(
                    new Point(0, lastContainer.Bounds.Height), TreeDropIndicatorCanvas);
                if (bounds.HasValue)
                {
                    Canvas.SetLeft(TreeDropLine, bounds.Value.X);
                    Canvas.SetTop(TreeDropLine, bounds.Value.Y);
                    TreeDropLine.Width = lastContainer.Bounds.Width;
                    TreeDropLine.IsVisible = true;
                    return;
                }
            }
        }

        TreeDropLine.IsVisible = false;
    }

    private TreeViewItem? FindLastVisibleTranslationContainer()
    {
        if (_imageTreeView == null || _draggedTreeItem == null) return null;
        var sourceParent = Navigation.GetParentImageItem(_draggedTreeItem);
        if (sourceParent == null) return null;

        TreeViewItem? last = null;
        double lastMidY = double.MinValue;

        foreach (var container in _imageTreeView.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (container.DataContext is not TranslationTreeItem ti) continue;
            if (Navigation.GetParentImageItem(ti) != sourceParent) continue;

            var bounds = container.TranslatePoint(new Point(0, 0), _imageTreeView);
            if (!bounds.HasValue) continue;

            double midY = bounds.Value.Y + container.Bounds.Height / 2;
            if (midY > lastMidY)
            {
                lastMidY = midY;
                last = container;
            }
        }

        return last;
    }

    private void CleanupTreeDragState()
    {
        _isTreeItemDragging = false;
        _isDragActive = false;
        _draggedTreeItem = null;
        _currentDropTarget = null;
        if (TreeDragPreview != null) TreeDragPreview.IsVisible = false;
        if (TreeDropLine != null) TreeDropLine.IsVisible = false;
        Cursor = null;
    }

    private void OnTreeDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CleanupTreeDragState();
        if (sender is Control control)
            control.PointerCaptureLost -= OnTreeDragPointerCaptureLost;
    }

    /// <summary>
    /// 执行拖拽重排：将 sourceItem 移动到 targetItem 的位置。
    /// 若 targetItem 为 null，则插入到列表末尾。
    /// </summary>
    private void PerformReorder(TranslationTreeItem sourceItem, TranslationTreeItem? targetItem)
    {
        var parentImageItem = Navigation.GetParentImageItem(sourceItem);
        if (parentImageItem == null) return;
        if (targetItem != null && Navigation.GetParentImageItem(targetItem) != parentImageItem) return;

        if (Document.TranslationData != null)
        {
            string imageName = parentImageItem.ImageName;
            if (Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            {
                var sourceModel = labels.FirstOrDefault(l => l.TextIndex == sourceItem.Index);
                if (sourceModel == null) return;

                int targetIndex;
                if (targetItem != null)
                {
                    var targetModel = labels.FirstOrDefault(l => l.TextIndex == targetItem.Index);
                    if (targetModel == null) return;
                    targetIndex = labels.IndexOf(targetModel);
                }
                else
                {
                    targetIndex = labels.Count;
                }

                CanvasWorkspace.ReorderLabels(labels, sourceModel, targetIndex, targetIndex + 1);
                // 修正 _pendingNewLabelIndex：末尾插入时 targetIndex+1 越界（超出 TextIndex 范围），
                // 导致 RebuildCurrentView 无法恢复选中，TreeView 滚回顶部。
                CanvasWorkspace.SetPendingNewLabelIndex(sourceModel.TextIndex);
            }
        }
    }
    
    // ==================== 文件拖放打开 ====================

}
