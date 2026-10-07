using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;
using LabelAva.Models;
using LabelAva.ViewModels;

namespace LabelAva.Views;

/// <summary>
/// 树视图拖拽重排的状态机。
///
/// 状态用**单个枚举**表示（Idle / Pending / Dragging），取代原先
/// <c>_isTreeItemDragging</c> + <c>_isDragActive</c> + <c>_draggedTreeItem</c>
/// 三个字段互相配合的写法 —— 那组字段能表达出「既是 Pending 又是 Dragging」这类非法组合，
/// 而且分散在三处 handler 里各自维护。
///
/// 只依赖构造时注入的控件与 ViewModel，不再读写 MainWindow 的私有字段。
/// </summary>
internal sealed class TreeDragDropController
{
    /// <summary>超过该像素位移才从 Pending 进入 Dragging。</summary>
    private const double DragThreshold = 4.0;

    private enum DragState
    {
        Idle,
        Pending,
        Dragging
    }

    private readonly Window _owner;
    private readonly TreeView _treeView;
    private readonly Canvas _dropIndicatorCanvas;
    private readonly Rectangle _dropLine;
    private readonly Canvas _previewCanvas;
    private readonly Border _preview;
    private readonly TextBlock _previewText;
    private readonly NavigationViewModel _navigation;
    private readonly DocumentViewModel _document;
    private readonly CanvasWorkspaceViewModel _canvasWorkspace;
    private readonly Func<bool> _requireEditMode;

    private DragState _state = DragState.Idle;
    private Point _startPoint;
    private TranslationTreeItem? _draggedItem;
    private TranslationTreeItem? _dropTarget;

    public TreeDragDropController(
        Window owner,
        TreeView treeView,
        NavigationViewModel navigation,
        DocumentViewModel document,
        CanvasWorkspaceViewModel canvasWorkspace,
        Func<bool> requireEditMode)
    {
        _owner = owner;
        _treeView = treeView;
        _navigation = navigation;
        _document = document;
        _canvasWorkspace = canvasWorkspace;
        _requireEditMode = requireEditMode;

        // 拖拽的可视反馈元素都在 MainWindow.axaml 里具名定义，缺失就快速失败
        _dropIndicatorCanvas = FindRequired<Canvas>(owner, "TreeDropIndicatorCanvas");
        _dropLine = FindRequired<Rectangle>(owner, "TreeDropLine");
        _previewCanvas = FindRequired<Canvas>(owner, "TreeDragPreviewCanvas");
        _preview = FindRequired<Border>(owner, "TreeDragPreview");
        _previewText = FindRequired<TextBlock>(owner, "TreeDragPreviewText");
    }

    /// <summary>是否处于拖拽中（选中联动逻辑据此跳过重建）。</summary>
    public bool IsDragging => _state == DragState.Dragging;

    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_state != DragState.Idle) return;

        var point = e.GetCurrentPoint(sender as Control);
        if (!point.Properties.IsLeftButtonPressed || sender is not Control control) return;

        // 仅允许拖拽 TranslationTreeItem（子节点）
        if (control.DataContext is not TranslationTreeItem treeItem) return;

        _startPoint = point.Position;
        _state = DragState.Pending;
        _draggedItem = treeItem;
        control.PointerCaptureLost += OnPointerCaptureLost;
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // 阶段1：Pending —— 阈值检测，决定是否进入拖拽
        if (_state == DragState.Pending && _draggedItem != null)
        {
            var point = e.GetCurrentPoint(sender as Control);
            var diff = point.Position - _startPoint;

            if (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold)
            {
                if (!_requireEditMode())
                {
                    _state = DragState.Idle;
                    _draggedItem = null;
                    return;
                }

                // 进入 Dragging 状态
                _state = DragState.Dragging;
                e.Pointer.Capture((Control)sender!);
                ShowPreview(_draggedItem);
                UpdatePreviewPos(e);
                _owner.Cursor = new Cursor(StandardCursorType.SizeAll);
            }

            return;
        }

        // 阶段2：Dragging —— 中点判定落点 + 预览位置 + 落点横线
        if (_state == DragState.Dragging && _draggedItem != null)
        {
            var treeViewPos = e.GetPosition(_treeView);
            var newTarget = GetDropTarget(treeViewPos);

            if (newTarget != _dropTarget)
            {
                _dropTarget = newTarget;
                UpdateDropLine(newTarget);
            }

            UpdatePreviewPos(e);
        }
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_state == DragState.Dragging && _draggedItem != null)
        {
            PerformReorder(_draggedItem, _dropTarget);
            e.Pointer.Capture(null);
        }

        Cleanup();
    }

    private void Cleanup()
    {
        _state = DragState.Idle;
        _draggedItem = null;
        _dropTarget = null;

        _preview.IsVisible = false;
        _dropLine.IsVisible = false;
        _owner.Cursor = null;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        Cleanup();
        if (sender is Control control)
            control.PointerCaptureLost -= OnPointerCaptureLost;
    }

    /// <summary>
    /// 在 TreeView 中通过中点判定查找有效的放置目标。
    /// 仅允许同图片下的 TranslationTreeItem 作为目标；返回 null 表示插入到列表末尾。
    /// </summary>
    private TranslationTreeItem? GetDropTarget(Point posInTreeView)
    {
        if (_draggedItem == null) return null;

        var sourceParent = _navigation.GetParentImageItem(_draggedItem);
        if (sourceParent == null) return null;

        var candidates = new List<(double MidY, TranslationTreeItem Item)>();

        foreach (var container in _treeView.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (container.DataContext is not TranslationTreeItem treeItem) continue;
            if (treeItem == _draggedItem) continue;
            if (_navigation.GetParentImageItem(treeItem) != sourceParent) continue;

            var bounds = container.TranslatePoint(new Point(0, 0), _treeView);
            if (!bounds.HasValue) continue;

            double midY = bounds.Value.Y + container.Bounds.Height / 2;
            candidates.Add((midY, treeItem));
        }

        candidates.Sort((a, b) => a.MidY.CompareTo(b.MidY));

        // 找到首个 midY 在光标下方的项 —— 即「在此项前插入」
        foreach (var (midY, treeItem) in candidates)
        {
            if (posInTreeView.Y < midY)
                return treeItem;
        }

        // 光标低于所有候选项 → 末尾插入
        return null;
    }

    private TreeViewItem? FindContainerForItem(TranslationTreeItem item)
        => _treeView.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .FirstOrDefault(c => c.DataContext == item);

    private void ShowPreview(TranslationTreeItem item)
    {
        _previewText.Text = item.Text;
        _preview.IsVisible = true;
    }

    private void UpdatePreviewPos(PointerEventArgs e)
    {
        var canvasPos = e.GetPosition(_previewCanvas);
        Canvas.SetLeft(_preview, canvasPos.X);
        Canvas.SetTop(_preview, canvasPos.Y);
    }

    private void UpdateDropLine(TranslationTreeItem? target)
    {
        if (target != null)
        {
            var container = FindContainerForItem(target);
            if (container != null)
            {
                var bounds = container.TranslatePoint(new Point(0, 0), _dropIndicatorCanvas);
                if (bounds.HasValue)
                {
                    Canvas.SetLeft(_dropLine, bounds.Value.X);
                    Canvas.SetTop(_dropLine, bounds.Value.Y);
                    _dropLine.Width = container.Bounds.Width;
                    _dropLine.IsVisible = true;
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
                    new Point(0, lastContainer.Bounds.Height), _dropIndicatorCanvas);
                if (bounds.HasValue)
                {
                    Canvas.SetLeft(_dropLine, bounds.Value.X);
                    Canvas.SetTop(_dropLine, bounds.Value.Y);
                    _dropLine.Width = lastContainer.Bounds.Width;
                    _dropLine.IsVisible = true;
                    return;
                }
            }
        }

        _dropLine.IsVisible = false;
    }

    private TreeViewItem? FindLastVisibleTranslationContainer()
    {
        if (_draggedItem == null) return null;

        var sourceParent = _navigation.GetParentImageItem(_draggedItem);
        if (sourceParent == null) return null;

        TreeViewItem? last = null;
        double lastMidY = double.MinValue;

        foreach (var container in _treeView.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (container.DataContext is not TranslationTreeItem ti) continue;
            if (_navigation.GetParentImageItem(ti) != sourceParent) continue;

            var bounds = container.TranslatePoint(new Point(0, 0), _treeView);
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

    /// <summary>
    /// 执行拖拽重排：将 sourceItem 移动到 targetItem 的位置；targetItem 为 null 表示插入到末尾。
    /// </summary>
    private void PerformReorder(TranslationTreeItem sourceItem, TranslationTreeItem? targetItem)
    {
        var parentImageItem = _navigation.GetParentImageItem(sourceItem);
        if (parentImageItem == null) return;
        if (targetItem != null && _navigation.GetParentImageItem(targetItem) != parentImageItem) return;

        if (_document.TranslationData == null) return;

        string imageName = parentImageItem.ImageName;
        if (!_document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels)) return;

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

        _canvasWorkspace.ReorderLabels(labels, sourceModel, targetIndex, targetIndex + 1);

        // 修正 _pendingNewLabelIndex：末尾插入时 targetIndex+1 越界（超出 TextIndex 范围），
        // 导致 RebuildCurrentView 无法恢复选中，TreeView 滚回顶部。
        _canvasWorkspace.SetPendingNewLabelIndex(sourceModel.TextIndex);
    }

    private static T FindRequired<T>(Window owner, string name) where T : Control
        => owner.FindControl<T>(name)
           ?? throw new InvalidOperationException($"拖拽控制器需要的控件 '{name}' 不存在");
}
