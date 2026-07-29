using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;

namespace LabelAva.Services;

/// <summary>
/// 为 ListBox 提供拖拽排序能力的辅助类。
/// 在 ListBox 的父 Panel 上自动创建拖拽预览和插入线覆盖层。
/// </summary>
/// <typeparam name="T">列表项数据类型</typeparam>
public class ListBoxDragReorderHelper<T> where T : class
{
    private readonly ListBox _listBox;
    private readonly ObservableCollection<T> _items;
    private readonly Func<T, string> _previewTextSelector;

    // 覆盖层元素
    private readonly Canvas _dropIndicatorCanvas;
    private readonly Rectangle _dropLine;
    private readonly Canvas _dragPreviewCanvas;
    private readonly Border _dragPreview;
    private readonly TextBlock _dragPreviewText;

    // 拖拽状态机
    private T? _dragItem;
    private bool _isPending;
    private bool _isDragging;
    private Point _dragStartPos;
    private const double DragThreshold = 4.0;

    /// <summary>是否正在拖拽中（供调用方在 SelectionChanged 等事件中判断）</summary>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// 创建拖拽排序辅助实例。
    /// </summary>
    /// <param name="listBox">目标 ListBox</param>
    /// <param name="items">列表数据源（ObservableCollection）</param>
    /// <param name="previewTextSelector">从列表项提取拖拽预览文本的函数</param>
    public ListBoxDragReorderHelper(ListBox listBox, ObservableCollection<T> items, Func<T, string> previewTextSelector)
    {
        _listBox = listBox;
        _items = items;
        _previewTextSelector = previewTextSelector;

        // 创建 DropLine（插入指示线）
        _dropLine = new Rectangle
        {
            Height = 2,
            Fill = Brushes.DodgerBlue,
            IsVisible = false
        };

        _dropIndicatorCanvas = new Canvas
        {
            IsHitTestVisible = false
        };
        _dropIndicatorCanvas.Children.Add(_dropLine);

        // 创建 DragPreview（拖拽预览浮层）
        _dragPreviewText = new TextBlock
        {
            FontSize = 13
        };

        _dragPreview = new Border
        {
            IsVisible = false,
            Opacity = 0.7,
            Background = Application.Current is { } app
                ? app.FindResource("SystemControlBackgroundChromeMediumLowBrush") as IBrush
                : Brushes.Gray,
            BorderBrush = Application.Current is { } app2
                ? app2.FindResource("SystemControlForegroundChromeHighBrush") as IBrush
                : Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 2),
            Child = _dragPreviewText
        };

        _dragPreviewCanvas = new Canvas
        {
            IsHitTestVisible = false
        };
        _dragPreviewCanvas.Children.Add(_dragPreview);

        // 将覆盖层添加到 ListBox 所在的 Panel 上。
        // ListBox 的直接父容器可能是 ScrollViewer 等非 Panel 元素，需向上遍历。
        AttachOverlays(listBox);

        void AttachOverlays(ListBox lb)
        {
            // 向上查找最近的 Panel 祖先
            var parent = lb.Parent;
            while (parent is not Panel && parent != null)
                parent = parent.Parent;

            if (parent is Panel parentPanel)
            {
                parentPanel.Children.Add(_dropIndicatorCanvas);
                parentPanel.Children.Add(_dragPreviewCanvas);
            }
            else
            {
                // 控件尚未挂载到可视树，等 Loaded 后再试
                lb.Loaded += (_, _) =>
                {
                    var p = lb.Parent;
                    while (p is not Panel && p != null)
                        p = p.Parent;
                    if (p is Panel pp)
                    {
                        pp.Children.Add(_dropIndicatorCanvas);
                        pp.Children.Add(_dragPreviewCanvas);
                    }
                };
            }
        }
    }

    // ========================
    // 公开事件处理器（供 DataTemplate 中委托）
    // ========================

    /// <summary>
    /// PointerPressed 处理器。应在 DataTemplate 外层元素的 PointerPressed 事件中调用。
    /// </summary>
    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_listBox).Properties.IsLeftButtonPressed) return;
        if (sender is not InputElement inputElement) return;
        if (inputElement.DataContext is not T item) return;

        _dragItem = item;
        _dragStartPos = e.GetPosition(_listBox);
        _isPending = true;
        _isDragging = false;
    }

    /// <summary>
    /// PointerMoved 处理器。应在 DataTemplate 外层元素的 PointerMoved 事件中调用。
    /// </summary>
    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPending && !_isDragging) return;
        if (_dragItem == null) return;

        var pos = e.GetPosition(_listBox);

        if (_isPending)
        {
            var delta = pos - _dragStartPos;
            if (Math.Abs(delta.Y) < DragThreshold) return;
            _isPending = false;
            _isDragging = true;

            if (sender is InputElement inputElement)
            {
                inputElement.PointerCaptureLost += OnDragPointerCaptureLost;
                e.Pointer.Capture(inputElement);
            }
            ShowPreview(_dragItem);
        }

        if (_isDragging)
        {
            UpdatePreviewPos(e);
            UpdateDropLine(pos);
        }

        e.Handled = true;
    }

    /// <summary>
    /// PointerReleased 处理器。应在 DataTemplate 外层元素的 PointerReleased 事件中调用。
    /// </summary>
    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _dragItem != null)
        {
            var pos = e.GetPosition(_listBox);
            int targetIndex = GetDropIndex(pos);
            PerformReorder(_dragItem, targetIndex);
        }

        CleanupDragState();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    // ========================
    // 内部实现
    // ========================

    private void OnDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CleanupDragState();
        if (sender is InputElement inputElement)
            inputElement.PointerCaptureLost -= OnDragPointerCaptureLost;
    }

    private void CleanupDragState()
    {
        _isPending = false;
        _isDragging = false;
        _dragItem = null;
        _dropLine.IsVisible = false;
        _dragPreview.IsVisible = false;
    }

    private void ShowPreview(T item)
    {
        _dragPreviewText.Text = _previewTextSelector(item);
        _dragPreview.IsVisible = true;
    }

    private void UpdatePreviewPos(PointerEventArgs e)
    {
        var canvasPos = e.GetPosition(_dragPreviewCanvas);
        Canvas.SetLeft(_dragPreview, canvasPos.X);
        Canvas.SetTop(_dragPreview, canvasPos.Y);
    }

    private int GetDropIndex(Point posInList)
    {
        if (_items is not { Count: > 0 }) return 0;

        for (int i = 0; i < _items.Count; i++)
        {
            var container = _listBox.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;

            var bounds = container.TranslatePoint(new Point(0, 0), _listBox);
            if (!bounds.HasValue) continue;

            double midY = bounds.Value.Y + container.Bounds.Height / 2;
            if (posInList.Y < midY) return i;
        }
        return _items.Count;
    }

    private void UpdateDropLine(Point posInList)
    {
        int targetIndex = GetDropIndex(posInList);

        if (targetIndex < _items.Count)
        {
            var container = _listBox.ContainerFromIndex(targetIndex) as ListBoxItem;
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
        else if (_items is { Count: > 0 })
        {
            // 末尾：放在最后一项的下方
            var container = _listBox.ContainerFromIndex(_items.Count - 1) as ListBoxItem;
            if (container != null)
            {
                var bounds = container.TranslatePoint(new Point(0, container.Bounds.Height), _dropIndicatorCanvas);
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

        _dropLine.IsVisible = false;
    }

    private void PerformReorder(T item, int targetIndex)
    {
        int fromIndex = _items.IndexOf(item);
        if (fromIndex < 0) return;

        // 调整目标索引：如果向后拖，实际索引需要 -1
        if (fromIndex < targetIndex) targetIndex--;

        if (fromIndex == targetIndex) return;

        _items.Move(fromIndex, targetIndex);
    }
}
