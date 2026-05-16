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
/// DataContext = CanvasWorkspaceViewModel（视口变换、标签操作、命中测试）
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
    private int? _draggingLabelIndex;
    
    // ========================
    // 私有字段 - 标注控件
    // ========================
    
    private List<Control> _labelControls = new();
    
    // ========================
    // 私有字段 - 设置缓存
    // ========================
    
    private AppSettingsProvider? _settingsProvider;

    public AppSettingsProvider? SettingsProvider
    {
        get => _settingsProvider;
        set => _settingsProvider = value;
    }
    
    // ========================
    // 私有字段 - 首次加载标志
    // ========================
    
    private bool _isFirstImageLoaded = false;
    
    // ========================
    // 公开回调属性（由 MainWindow 注入）
    // ========================
    
    /// <summary>是否允许拖拽标注（编辑模式为 true，查看模式为 false）</summary>
    public bool IsEditMode { get; set; }

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
    
    /// <summary>标签拖拽结束后位置变更（通知 MainWindow 保存到数据模型）</summary>
    /// <remarks>参数为 (textIndex, oldNormX, oldNormY, newNormX, newNormY)</remarks>
    public event EventHandler<(int textIndex, double oldNormX, double oldNormY, double newNormX, double newNormY)>? LabelMoved;
    
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
    
    /// <summary>加载图片（仅处理 Bitmap 加载 + CanvasWorkspace 尺寸通知）</summary>
    public void LoadImage(string imagePath)
    {
        try
        {
            ClearErrorPlaceholder();
            
            if (_currentImage != null)
            {
                _currentImage.Dispose();
            }
            
            _currentImage = new Bitmap(imagePath);
            _currentImagePath = imagePath;
            
            // 首次加载时隐藏 ImageWrapper，避免在 Fit 变换计算完成前以 Identity 矩阵渲染导致缩放跳变
            if (!_isFirstImageLoaded)
            {
                ImageWrapper.Opacity = 0;
            }
            
            // 设置图片源
            MainImage.Source = _currentImage;
        }
        catch (Exception ex)
        {
            _currentImage = null;
            _currentImagePath = null;
            throw new InvalidOperationException($" {ex.Message}", ex);
        }
    }
    
    /// <summary>重建标注 Border 控件</summary>
    public void UpdateLabels(List<LabelItem> labels, double imageWidth, double imageHeight, int? highlightIndex)
    {
        // 移除旧的标注（解绑事件）
        ClearLabelControls();
        
        if (imageWidth <= 0 || imageHeight <= 0)
            return;
        
        int labelSize = GetLabelSize();
        double halfSize = labelSize / 2.0;
        var matrix = (DataContext as CanvasWorkspaceViewModel)?.TransformMatrix ?? Matrix.Identity;
        
        foreach (var label in labels)
        {
            // 根据分组获取对应的背景颜色
            var groupBrush = GetGroupBrush(label.GroupIndex);

            var border = new Border
            {
                Width = labelSize,
                Height = labelSize,
                Background = groupBrush,
                CornerRadius = new CornerRadius(labelSize / 4.0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                // 存储元组 (TextIndex, GroupIndex, NormX, NormY) 用于后续高亮匹配和位置更新
                Tag = (label.TextIndex, label.GroupIndex, label.X, label.Y),
                Classes = { "label-marker" },
                Child = new TextBlock
                {
                    Text = label.TextIndex.ToString(),
                    Foreground = Brushes.White,
                    FontSize = labelSize * 0.75,
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
            
            // 通过变换矩阵将图像像素坐标转换为屏幕坐标定位标签
            var screenPoint = matrix.Transform(new Point(label.X * imageWidth, label.Y * imageHeight));
            Canvas.SetLeft(border, screenPoint.X - halfSize);
            Canvas.SetTop(border, screenPoint.Y - halfSize);
            
            border.ZIndex = 10;
            
            // 标签添加到 ImageCanvas（不受 ImageWrapper 的 RenderTransform 影响）
            ImageCanvas.Children.Add(border);
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
                // Tag 是一个元组 (TextIndex, GroupIndex, NormX, NormY)
                if (border.Tag is ValueTuple<int, int, double, double> tag && tag.Item1 == labelIndex)
                {
                    // 高亮状态
                    border.Background = selectedBrush;
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(3);
                    border.ZIndex = 20;
                }
                else if (border.Tag is ValueTuple<int, int, double, double> normalTag)
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
    
    /// <summary>同步 CanvasWorkspace.TransformMatrix 到 UI</summary>
    public void ApplyTransform()
    {
        if (_matrixTransform != null && DataContext is CanvasWorkspaceViewModel CanvasWorkspace)
        {
            _matrixTransform.Matrix = CanvasWorkspace.TransformMatrix;
        }
        // 标签在 ImageCanvas 上不受 RenderTransform 影响，需要手动更新位置
        UpdateLabelPositions();
    }
    
    /// <summary>计算 Fit 变换（委托给 CanvasWorkspace）</summary>
    public void CalculateFitTransform()
    {
        if (_currentImage == null || DataContext is not CanvasWorkspaceViewModel CanvasWorkspace)
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
        
        // 通知 CanvasWorkspace 容器和图片尺寸，然后计算 Fit 变换
        CanvasWorkspace.UpdateContainerSize(new Size(containerBounds.Width, containerBounds.Height));
        CanvasWorkspace.UpdateImageSize(new Size(_currentImage.Size.Width, _currentImage.Size.Height));
        CanvasWorkspace.CalculateFitTransform();
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
        _isFirstImageLoaded = false;
        ClearLabelControls();
        MainImage.Source = null;
        // 重置可见性，确保下次首次加载时隐藏逻辑正常工作
        ImageWrapper.Opacity = 1;
        ClearErrorPlaceholder();
    }
    
    /// <summary>显示错误占位符（图片无法加载时调用）</summary>
    public void ShowErrorPlaceholder(string imageName)
    {
        ClearErrorPlaceholder();
        
        _currentImage = null;
        _currentImagePath = imageName;
        MainImage.Source = null;
        
        ErrorOverlayIcon.Text = "\u26a0";
        ErrorOverlayText.Text = $"\u627e\u4e0d\u5230\u6307\u5b9a\u7684\u6587\u4ef6: {imageName}";
        ErrorOverlay.IsVisible = true;
    }
    
    /// <summary>清除错误占位符</summary>
    private void ClearErrorPlaceholder()
    {
        ErrorOverlay.IsVisible = false;
    }
    
    /// <summary>标记首次加载已完成（MainWindow 在首次 FitTransform 后调用）</summary>
    public void MarkFirstImageLoaded()
    {
        _isFirstImageLoaded = true;
        // Fit 变换已计算完成，恢复 ImageWrapper 可见性
        ImageWrapper.Opacity = 1;
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
            ImageCanvas.Children.Remove(control);
        }
        _labelControls.Clear();
    }
    
    /// <summary>获取当前标签大小（像素），从设置中读取并限制范围</summary>
    private int GetLabelSize()
    {
        return Math.Clamp(CurrentSettings.LabelSize, 24, 128);
    }

    private AppSettings CurrentSettings => _settingsProvider?.Current ?? AppSettings.CreateDefaults();
    
    /// <summary>根据当前变换矩阵重新计算所有标签的屏幕位置</summary>
    public void UpdateLabelPositions()
    {
        if (_currentImage == null || DataContext is not CanvasWorkspaceViewModel vm)
            return;

        var matrix = vm.TransformMatrix;
        double imageWidth = _currentImage.Size.Width;
        double imageHeight = _currentImage.Size.Height;
        double halfSize = GetLabelSize() / 2.0;

        foreach (var control in _labelControls)
        {
            if (control is Border border &&
                border.Tag is ValueTuple<int, int, double, double> tag)
            {
                // 跳过正在拖拽的标签
                if (_isDraggingLabel && border == _draggedLabel)
                    continue;

                double normX = tag.Item3;
                double normY = tag.Item4;

                var screenPoint = matrix.Transform(
                    new Point(normX * imageWidth, normY * imageHeight));

                Canvas.SetLeft(border, screenPoint.X - halfSize);
                Canvas.SetTop(border, screenPoint.Y - halfSize);
            }
        }
    }
    
    /// <summary>更新缓存的快捷键设置（由 MainWindow 在设置变更时调用）</summary>
    public void UpdateSettings(AppSettings settings)
    {
        if (DataContext is CanvasWorkspaceViewModel vm)
            vm.LabelSize = settings.LabelSize;
    }
    
    /// <summary>获取指定分组的背景颜色</summary>
    private IBrush GetGroupBrush(int groupIndex)
    {
        var colors = CurrentSettings.Colors;

        if (colors.GroupColors.TryGetValue(groupIndex, out var colorHex) &&
            !string.IsNullOrEmpty(colorHex) && colorHex.StartsWith("#"))
        {
            try
            {
                var color = Avalonia.Media.Color.Parse(colorHex);
                return new SolidColorBrush(color, 0.8);
            }
            catch { }
        }

        var defaults = ColorSettings.CreateDefaults();
        if (defaults.GroupColors.TryGetValue(groupIndex, out var defaultColorHex))
        {
            try
            {
                var color = Avalonia.Media.Color.Parse(defaultColorHex);
                return new SolidColorBrush(color, 0.8);
            }
            catch { }
        }

        return new SolidColorBrush(Colors.White, 0.8);
    }

    /// <summary>获取当前设置中的选中高亮颜色</summary>
    private IBrush GetSelectedHighlightBrush()
    {
        var colors = CurrentSettings.Colors;
        var selectedColorHex = colors.SelectedColor;

        if (!string.IsNullOrEmpty(selectedColorHex) && selectedColorHex.StartsWith("#"))
        {
            try
            {
                var color = Avalonia.Media.Color.Parse(selectedColorHex);
                return new SolidColorBrush(color, 0.9);
            }
            catch { }
        }

        var defaults = ColorSettings.CreateDefaults();
        if (!string.IsNullOrEmpty(defaults.SelectedColor))
        {
            try
            {
                var color = Avalonia.Media.Color.Parse(defaults.SelectedColor);
                return new SolidColorBrush(color, 0.9);
            }
            catch { }
        }

        return new SolidColorBrush(Colors.White, 0.9);
    }
    
    // ========================
    // 事件处理 - 容器尺寸
    // ========================
    
    private void OnImageContainerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_currentImage == null || DataContext is not CanvasWorkspaceViewModel CanvasWorkspace) 
            return;
        
        // 通知 CanvasWorkspace 容器尺寸变化
        CanvasWorkspace.UpdateContainerSize(new Size(e.NewSize.Width, e.NewSize.Height));
        CanvasWorkspace.OnContainerSizeChanged();
    }
    
    // ========================
    // 事件处理 - 标签标记交互
    // ========================
    
    private void OnLabelMarkerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border)
            return;

        int? labelIndex = null;
        if (border.Tag is ValueTuple<int, int, double, double> tuple)
            labelIndex = tuple.Item1;
        else if (border.Tag is int intIndex)
            labelIndex = intIndex;

        if (!labelIndex.HasValue)
            return;

        var point = e.GetCurrentPoint(ImageCanvas);
        if (point.Properties.IsLeftButtonPressed)
        {
            e.Handled = true;

            // 发送标签被点击事件（查看/编辑模式均可）
            LabelClicked?.Invoke(this, labelIndex.Value);

            if (!IsEditMode)
                return;

            // 编辑模式下：提交编辑、启动拖拽
            CommitCurrentEdit?.Invoke();

            _isDraggingLabel = true;
            _draggedLabel = border;
            _labelDragLastPoint = point.Position;
            e.Pointer.Capture(border);

            // 从 Tag 中读取归一化坐标作为拖拽起始位置
            if (border.Tag is ValueTuple<int, int, double, double> tag)
            {
                _dragStartNormX = tag.Item3;
                _dragStartNormY = tag.Item4;
                _draggingLabelIndex = labelIndex.Value;
            }
        }
    }

    private void OnLabelMarkerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingLabel && _draggedLabel != null && sender == _draggedLabel)
        {
            e.Handled = true;
            
            var currentPoint = e.GetPosition(ImageCanvas);
            var delta = currentPoint - _labelDragLastPoint;
            
            double currentLeft = Canvas.GetLeft(_draggedLabel);
            double currentTop = Canvas.GetTop(_draggedLabel);
            
            double newLeft = currentLeft + delta.X;
            double newTop = currentTop + delta.Y;
            double halfSize = GetLabelSize() / 2.0;
            
            // 通过逆矩阵在图像空间中做边界检查
            if (_currentImage != null && DataContext is CanvasWorkspaceViewModel vm)
            {
                var inverse = vm.TransformMatrix.Invert();
                var imagePoint = inverse.Transform(new Point(newLeft + halfSize, newTop + halfSize));
                double imgW = _currentImage.Size.Width;
                double imgH = _currentImage.Size.Height;
                imagePoint = new Point(
                    Math.Clamp(imagePoint.X, 0, imgW),
                    Math.Clamp(imagePoint.Y, 0, imgH));
                var clampedScreen = vm.TransformMatrix.Transform(imagePoint);
                newLeft = clampedScreen.X - halfSize;
                newTop = clampedScreen.Y - halfSize;
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
            var point = e.GetCurrentPoint(ImageCanvas);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                e.Handled = true;
                
                // 通过逆矩阵将屏幕坐标转换为图像像素坐标，再计算归一化坐标
                if (_currentImage != null && _draggingLabelIndex.HasValue && DataContext is CanvasWorkspaceViewModel vm)
                {
                    double finalLeft = Canvas.GetLeft(_draggedLabel);
                    double finalTop = Canvas.GetTop(_draggedLabel);
                    double halfSize = GetLabelSize() / 2.0;
                    var inverse = vm.TransformMatrix.Invert();
                    var imagePoint = inverse.Transform(new Point(finalLeft + halfSize, finalTop + halfSize));
                    double newNormX = imagePoint.X / _currentImage.Size.Width;
                    double newNormY = imagePoint.Y / _currentImage.Size.Height;
                    
                    // 更新 Tag 中的归一化坐标
                    if (_draggedLabel.Tag is ValueTuple<int, int, double, double> oldTag)
                        _draggedLabel.Tag = (oldTag.Item1, oldTag.Item2, newNormX, newNormY);
                    
                    // 只有位置实际发生变化时才触发事件
                    double dx = Math.Abs(newNormX - _dragStartNormX);
                    double dy = Math.Abs(newNormY - _dragStartNormY);
                    if (dx > 0.0001 || dy > 0.0001)
                    {
                        LabelMoved?.Invoke(this, (_draggingLabelIndex.Value, _dragStartNormX, _dragStartNormY, newNormX, newNormY));
                    }
                }
                
                _isDraggingLabel = false;
                _draggedLabel = null;
                _draggingLabelIndex = null;
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
            if (DataContext is CanvasWorkspaceViewModel CanvasWorkspace)
            {
                CanvasWorkspace.StartPan(e.GetPosition(ImageContainer));
                ImageContainer.Cursor = new Cursor(StandardCursorType.Hand);
            }
            e.Handled = true;
        }
    }

    private void OnImageContainerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is CanvasWorkspaceViewModel CanvasWorkspace && CanvasWorkspace.IsPanning)
        {
            CanvasWorkspace.UpdatePan(e.GetPosition(ImageContainer));
        }
    }

    private void OnImageContainerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is CanvasWorkspaceViewModel CanvasWorkspace && CanvasWorkspace.IsPanning)
        {
            CanvasWorkspace.EndPan();
            ImageContainer.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    private void OnImageContainerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_currentImage == null || DataContext is not CanvasWorkspaceViewModel CanvasWorkspace)
            return;
        
        // 获取鼠标在容器中的位置
        var mousePos = e.GetPosition(ImageContainer);
        
        // 确定缩放因子
        double zoom = e.Delta.Y > 0 ? 1.1 : 0.9;
        
        // 以鼠标为中心进行缩放
        CanvasWorkspace.ApplyZoomDelta(zoom, mousePos);
        
        e.Handled = true;
    }
}
