using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabelAva.Models;
using LabelAva.Services;
using LabelAva.ViewModels;

namespace LabelAva.Views;

/// <summary>
/// AnnotationCanvas UserControl - 封装画布区域的视觉渲染和交互逻辑
/// DataContext = CanvasViewModel（视口变换、标签操作、命中测试）
/// 采用回调+事件模式处理跨 VM 协调，避免 UserControl 直接依赖其他 VM
/// </summary>
public partial class AnnotationCanvas : UserControl
{
    // ========================
    // 私有字段 - 图片相关
    // ========================
    
    private Bitmap? _currentImage;
    private string? _currentImagePath;
    
    // ========================
    // 私有字段 - 矩阵变换
    // ========================
    
    private MatrixTransform? _matrixTransform;
    
    // ========================
    // 私有字段 - 拖拽交互
    // ========================
    
    private bool _isDraggingLabel = false;
    private Border? _draggedLabel;
    private Point _labelDragLastPoint;
    private double _dragStartNormX = 0;
    private double _dragStartNormY = 0;
    private LabelItem? _draggingLabelItem;
    
    // ========================
    // 私有字段 - 标注控件
    // ========================
    
    private List<Control> _labelControls = new();
    
    // ========================
    // 私有字段 - 首次加载标志
    // ========================
    
    private bool _isFirstImageLoaded = false;
    
    // ========================
    // 公开回调属性（由 MainWindow 注入）
    // ========================
    
    /// <summary>提交当前文本编辑（需要访问 _translationTextBox）</summary>
    public Action? CommitCurrentEdit { get; set; }
    
    /// <summary>选中指定索引的标签（需要协调 Navigation + StatusBar + 焦点）</summary>
    public Action<int>? SelectLabelByIndex { get; set; }
    
    // ========================
    // 公开事件（MainWindow 订阅）
    // ========================
    
    /// <summary>标签标记被点击（用户想选中它）</summary>
    public event EventHandler<int>? LabelClicked;
    
    /// <summary>用户在编辑模式下点击了空白位置，请求添加新标签</summary>
    /// <remarks>参数为 (pixelX, pixelY) 相对于原图的像素坐标</remarks>
    public event EventHandler<(double pixelX, double pixelY)>? AddLabelRequested;
    
    // ========================
    // 公开状态属性
    // ========================
    
    /// <summary>是否正在拖拽标签（RebuildCurrentView 需要检查）</summary>
    public bool IsDraggingLabel => _isDraggingLabel;
    
    /// <summary>当前图片路径</summary>
    public string? CurrentImagePath => _currentImagePath;
    
    /// <summary>当前图片 Bitmap</summary>
    public Bitmap? CurrentImage => _currentImage;
    
    /// <summary>是否首次加载</summary>
    public bool IsFirstImageLoaded => _isFirstImageLoaded;
    
    public AnnotationCanvas()
    {
        InitializeComponent();
        
        // 初始化 MatrixTransform
        _matrixTransform = ImageWrapper.RenderTransform as MatrixTransform;
        if (_matrixTransform == null)
        {
            _matrixTransform = new MatrixTransform();
            ImageWrapper.RenderTransform = _matrixTransform;
        }
    }
    
    // ========================
    // 公开方法（由 MainWindow 调用）
    // ========================
    
    /// <summary>加载图片（仅处理 Bitmap 加载 + CanvasVM 尺寸通知）</summary>
    public void LoadImage(string imagePath)
    {
        try
        {
            if (_currentImage != null)
            {
                _currentImage.Dispose();
            }
            
            _currentImage = new Bitmap(imagePath);
            _currentImagePath = imagePath;
            
            // 设置图片源
            MainImage.Source = _currentImage;
        }
        catch (Exception ex)
        {
            _currentImage = null;
            _currentImagePath = null;
            throw new InvalidOperationException($"加载图片失败: {ex.Message}", ex);
        }
    }
    
    /// <summary>重建标注 Border 控件</summary>
    public void UpdateLabels(List<LabelItem> labels, double imageWidth, double imageHeight, int? highlightIndex)
    {
        // 移除旧的标注（解绑事件）
        ClearLabelControls();
        
        if (imageWidth <= 0 || imageHeight <= 0)
            return;
        
        foreach (var label in labels)
        {
            // 根据分组获取对应的背景颜色
            var groupBrush = GetGroupBrush(label.GroupIndex);

            var border = new Border
            {
                Width = 64,
                Height = 64,
                Background = groupBrush,
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                // 存储元组 (TextIndex, GroupIndex) 用于后续高亮匹配
                Tag = (label.TextIndex, label.GroupIndex),
                Classes = { "label-marker" },
                Child = new TextBlock
                {
                    Text = label.TextIndex.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 48,
                    FontFamily = new FontFamily("Sarasa Mono SC"),
                    FontWeight = FontWeight.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            
            // 添加拖拽及点击事件
            border.PointerPressed += OnLabelMarkerPointerPressed;
            border.PointerMoved += OnLabelMarkerPointerMoved;
            border.PointerReleased += OnLabelMarkerPointerReleased;
            
            // 设置归一化坐标对应的像素位置
            Canvas.SetLeft(border, label.X * imageWidth - 32);
            Canvas.SetTop(border, label.Y * imageHeight - 32);
            
            border.ZIndex = 10;
            
            ImageWrapper.Children.Add(border);
            _labelControls.Add(border);
        }
        
        // 如果指定了高亮索引，则高亮它
        if (highlightIndex.HasValue)
        {
            HighlightLabel(highlightIndex.Value);
        }
    }
    
    /// <summary>高亮指定标签（-1 取消高亮）</summary>
    public void HighlightLabel(int labelIndex)
    {
        var selectedBrush = GetSelectedHighlightBrush();

        foreach (var control in _labelControls)
        {
            if (control is Border border)
            {
                // Tag 是一个元组 (TextIndex, GroupIndex)
                if (border.Tag is ValueTuple<int, int> tag && tag.Item1 == labelIndex)
                {
                    // 高亮状态
                    border.Background = selectedBrush;
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(3);
                    border.ZIndex = 20;
                }
                else if (border.Tag is ValueTuple<int, int> normalTag)
                {
                    // 普通状态
                    var groupIndex = normalTag.Item2;
                    border.Background = GetGroupBrush(groupIndex);
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                    border.ZIndex = 10;
                }
            }
        }
    }
    
    /// <summary>同步 CanvasVM.TransformMatrix 到 UI</summary>
    public void ApplyTransform()
    {
        if (_matrixTransform != null && DataContext is CanvasViewModel canvasVM)
        {
            _matrixTransform.Matrix = canvasVM.TransformMatrix;
        }
    }
    
    /// <summary>计算 Fit 变换（委托给 CanvasVM）</summary>
    public void CalculateFitTransform()
    {
        if (_currentImage == null || DataContext is not CanvasViewModel canvasVM)
            return;
        
        var containerBounds = new Rect(0, 0, ImageContainer.Bounds.Width, ImageContainer.Bounds.Height);
        if (containerBounds.Width <= 0 || containerBounds.Height <= 0)
        {
            // 容器未准备好，重试
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.Post(CalculateFitTransform);
            });
            return;
        }
        
        // 通知 CanvasVM 容器和图片尺寸，然后计算 Fit 变换
        canvasVM.UpdateContainerSize(new Size(containerBounds.Width, containerBounds.Height));
        canvasVM.UpdateImageSize(new Size(_currentImage.Size.Width, _currentImage.Size.Height));
        canvasVM.CalculateFitTransform();
    }
    
    /// <summary>清空画布（图片 + 标注）</summary>
    public void ClearCanvas()
    {
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        _currentImagePath = null;
        ClearLabelControls();
        MainImage.Source = null;
    }
    
    /// <summary>标记首次加载已完成（MainWindow 在首次 FitTransform 后调用）</summary>
    public void MarkFirstImageLoaded()
    {
        _isFirstImageLoaded = true;
    }
    
    // ========================
    // 私有帮助方法 - 标注相关
    // ========================
    
    private void ClearLabelControls()
    {
        foreach (var control in _labelControls)
        {
            if (control is Border border)
            {
                border.PointerPressed -= OnLabelMarkerPointerPressed;
                border.PointerMoved -= OnLabelMarkerPointerMoved;
                border.PointerReleased -= OnLabelMarkerPointerReleased;
            }
            ImageWrapper.Children.Remove(control);
        }
        _labelControls.Clear();
    }
    
    /// <summary>获取指定分组的背景颜色</summary>
    private IBrush GetGroupBrush(int groupIndex)
    {
        try
        {
            var settings = ShortcutSettingsService.Load();

            if (settings.Colors.GroupColors.TryGetValue(groupIndex, out var colorHex) &&
                !string.IsNullOrEmpty(colorHex) && colorHex.StartsWith("#"))
            {
                var color = Avalonia.Media.Color.Parse(colorHex);
                return new SolidColorBrush(color, 0.8);
            }

            // 如果没有找到对应颜色，从默认设置中获取
            var defaults = ColorSettings.CreateDefaults();
            if (defaults.GroupColors.TryGetValue(groupIndex, out var defaultColorHex))
            {
                var color = Avalonia.Media.Color.Parse(defaultColorHex);
                return new SolidColorBrush(color, 0.8);
            }
        }
        catch
        {
            // 如果出错，从默认设置中获取
            try
            {
                var defaults = ColorSettings.CreateDefaults();
                if (defaults.GroupColors.TryGetValue(groupIndex, out var defaultColorHex))
                {
                    var color = Avalonia.Media.Color.Parse(defaultColorHex);
                    return new SolidColorBrush(color, 0.8);
                }
            }
            catch
            {
                // 如果还是出错，使用白色
            }
        }

        return new SolidColorBrush(Colors.White, 0.8);
    }

    /// <summary>获取当前设置中的选中高亮颜色</summary>
    private IBrush GetSelectedHighlightBrush()
    {
        try
        {
            var settings = ShortcutSettingsService.Load();
            var selectedColorHex = settings.Colors.SelectedColor;

            if (!string.IsNullOrEmpty(selectedColorHex) && selectedColorHex.StartsWith("#"))
            {
                var color = Avalonia.Media.Color.Parse(selectedColorHex);
                return new SolidColorBrush(color, 0.9);
            }

            // 如果没有设置，从默认中获取
            var defaults = ColorSettings.CreateDefaults();
            if (!string.IsNullOrEmpty(defaults.SelectedColor))
            {
                var color = Avalonia.Media.Color.Parse(defaults.SelectedColor);
                return new SolidColorBrush(color, 0.9);
            }
        }
        catch
        {
            // 如果出错，尝试从默认中获取
            try
            {
                var defaults = ColorSettings.CreateDefaults();
                if (!string.IsNullOrEmpty(defaults.SelectedColor))
                {
                    var color = Avalonia.Media.Color.Parse(defaults.SelectedColor);
                    return new SolidColorBrush(color, 0.9);
                }
            }
            catch
            {
                // 如果还是出错，使用白色
            }
        }

        return new SolidColorBrush(Colors.White, 0.9);
    }
    
    // ========================
    // 事件处理 - 容器尺寸
    // ========================
    
    private void OnImageContainerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_currentImage == null || DataContext is not CanvasViewModel canvasVM) 
            return;
        
        // 通知 CanvasVM 容器尺寸变化
        canvasVM.UpdateContainerSize(new Size(e.NewSize.Width, e.NewSize.Height));
        canvasVM.OnContainerSizeChanged();
    }
    
    // ========================
    // 事件处理 - 标签标记交互
    // ========================
    
    private void OnLabelMarkerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border)
            return;

        int? labelIndex = null;
        if (border.Tag is ValueTuple<int, int> tuple)
            labelIndex = tuple.Item1;
        else if (border.Tag is int intIndex)
            labelIndex = intIndex;

        if (!labelIndex.HasValue)
            return;

        var point = e.GetCurrentPoint(ImageWrapper);
        if (point.Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            
            // 提交编辑并发送选择事件
            CommitCurrentEdit?.Invoke();

            _isDraggingLabel = true;
            _draggedLabel = border;
            _labelDragLastPoint = point.Position;
            e.Pointer.Capture(border);

            // 发送标签被点击事件
            LabelClicked?.Invoke(this, labelIndex.Value);
        }
    }

    private void OnLabelMarkerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingLabel && _draggedLabel != null && sender == _draggedLabel)
        {
            e.Handled = true;
            
            var currentPoint = e.GetPosition(ImageWrapper);
            var delta = currentPoint - _labelDragLastPoint;
            
            double currentLeft = Canvas.GetLeft(_draggedLabel);
            double currentTop = Canvas.GetTop(_draggedLabel);
            
            double newLeft = currentLeft + delta.X;
            double newTop = currentTop + delta.Y;
            
            // 限制在图片范围内
            if (_currentImage != null)
            {
                double minLeft = -32;
                double maxLeft = Math.Max(-32, _currentImage.Size.Width - 32);
                double minTop = -32;
                double maxTop = Math.Max(-32, _currentImage.Size.Height - 32);
                
                newLeft = Math.Clamp(newLeft, minLeft, maxLeft);
                newTop = Math.Clamp(newTop, minTop, maxTop);
            }
            
            Canvas.SetLeft(_draggedLabel, newLeft);
            Canvas.SetTop(_draggedLabel, newTop);
            
            _labelDragLastPoint = currentPoint;
        }
    }

    private void OnLabelMarkerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingLabel && _draggedLabel != null && sender == _draggedLabel)
        {
            var point = e.GetCurrentPoint(ImageWrapper);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                e.Handled = true;
                
                _isDraggingLabel = false;
                _draggedLabel = null;
                _draggingLabelItem = null;
                e.Pointer.Capture(null);
            }
        }
    }
    
    // ========================
    // 事件处理 - 容器交互
    // ========================
    
    private void OnImageContainerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(ImageContainer);
        
        if (point.Properties.IsLeftButtonPressed)
        {
            var imagePoint = e.GetPosition(MainImage);
            
            if (_currentImage != null && 
                imagePoint.X >= 0 && imagePoint.X <= _currentImage.Size.Width &&
                imagePoint.Y >= 0 && imagePoint.Y <= _currentImage.Size.Height)
            {
                // 提交编辑
                CommitCurrentEdit?.Invoke();
                
                // 发送添加标签请求（主窗口负责检查是否有现有标签）
                AddLabelRequested?.Invoke(this, (imagePoint.X, imagePoint.Y));
            }
            
            e.Handled = true;
        }
        // 允许任何模式下使用中键或右键平移
        else if (point.Properties.IsMiddleButtonPressed || point.Properties.IsRightButtonPressed)
        {
            if (DataContext is CanvasViewModel canvasVM)
            {
                canvasVM.StartPan(e.GetPosition(ImageContainer));
                ImageContainer.Cursor = new Cursor(StandardCursorType.Hand);
            }
            e.Handled = true;
        }
    }

    private void OnImageContainerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is CanvasViewModel canvasVM && canvasVM.IsPanning)
        {
            canvasVM.UpdatePan(e.GetPosition(ImageContainer));
        }
    }

    private void OnImageContainerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is CanvasViewModel canvasVM && canvasVM.IsPanning)
        {
            canvasVM.EndPan();
            ImageContainer.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    private void OnImageContainerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_currentImage == null || DataContext is not CanvasViewModel canvasVM)
            return;
        
        // 获取鼠标在容器中的位置
        var mousePos = e.GetPosition(ImageContainer);
        
        // 确定缩放因子
        double zoom = e.Delta.Y > 0 ? 1.1 : 0.9;
        
        // 以鼠标为中心进行缩放
        canvasVM.ApplyZoomDelta(zoom, mousePos);
        
        e.Handled = true;
    }
}
