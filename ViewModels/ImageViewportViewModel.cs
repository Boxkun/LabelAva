using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabelAva.ViewModels;

/// <summary>
/// 图片视口 ViewModel：封装缩放/平移/Fit 逻辑、变换矩阵管理。
/// 纯数学运算在 VM 内完成，UI 数据（容器尺寸、图片尺寸、鼠标位置）由 code-behind 注入。
/// </summary>
public partial class ImageViewportViewModel : ObservableObject
{
    // ========================
    // 状态属性
    // ========================

    /// <summary>变换矩阵</summary>
    [ObservableProperty]
    private Matrix _transformMatrix = Matrix.Identity;

    /// <summary>当前缩放百分比（如 100 表示 100%）</summary>
    [ObservableProperty]
    private double _zoomPercent = 100;

    /// <summary>当前 Fit 缩放比例（用于判断是否需要 CenterOnLabel）</summary>
    [ObservableProperty]
    private double _currentFitScale = 1.0;

    /// <summary>是否有图片加载</summary>
    [ObservableProperty]
    private bool _hasImage = false;

    // ========================
    // 内部状态
    // ========================

    // 容器尺寸（由 code-behind 通过 UpdateContainerSize 设置）
    private Size _containerSize;

    // 图片尺寸（由 code-behind 通过 UpdateImageSize 设置）
    private Size _imageSize;

    // 平移状态
    private bool _isPanning = false;
    private Point _lastPanPoint;

    // ========================
    // 命令
    // ========================

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void ZoomIn()
    {
        if (_containerSize.Width <= 0 || _containerSize.Height <= 0) return;
        var centerPoint = new Point(_containerSize.Width / 2, _containerSize.Height / 2);
        ApplyZoomDelta(1.2, centerPoint);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void ZoomOut()
    {
        if (_containerSize.Width <= 0 || _containerSize.Height <= 0) return;
        var centerPoint = new Point(_containerSize.Width / 2, _containerSize.Height / 2);
        ApplyZoomDelta(0.9, centerPoint);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void ResetZoom()
    {
        CalculateFitTransform();
    }

    // ========================
    // 公开方法（由 code-behind 调用）
    // ========================

    /// <summary>更新容器尺寸（在 SizeChanged 和初始化时调用）</summary>
    public void UpdateContainerSize(Size size)
    {
        _containerSize = size;
    }

    /// <summary>更新图片尺寸（在图片加载后调用）</summary>
    public void UpdateImageSize(Size size)
    {
        _imageSize = size;
        HasImage = size.Width > 0 && size.Height > 0;
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        ResetZoomCommand.NotifyCanExecuteChanged();
    }

    /// <summary>应用缩放增量（滚轮/菜单调用）</summary>
    public void ApplyZoomDelta(double zoomFactor, Point centerPoint)
    {
        TransformMatrix = ApplyZoom(TransformMatrix, zoomFactor, centerPoint);
        TransformMatrix = ApplyCentering(TransformMatrix);
        UpdateZoomPercent();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>开始平移</summary>
    public void StartPan(Point position)
    {
        _isPanning = true;
        _lastPanPoint = position;
    }

    /// <summary>更新平移（鼠标移动时调用）</summary>
    public void UpdatePan(Point currentPosition)
    {
        if (!_isPanning) return;

        var delta = currentPosition - _lastPanPoint;
        TransformMatrix = new Matrix(
            TransformMatrix.M11, TransformMatrix.M12,
            TransformMatrix.M21, TransformMatrix.M22,
            TransformMatrix.M31 + delta.X, TransformMatrix.M32 + delta.Y);

        TransformMatrix = ApplyCentering(TransformMatrix);
        _lastPanPoint = currentPosition;

        UpdateZoomPercent();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>结束平移</summary>
    public void EndPan()
    {
        _isPanning = false;
    }

    /// <summary>是否正在平移中</summary>
    public bool IsPanning => _isPanning;

    /// <summary>计算 Fit 自适应变换</summary>
    public void CalculateFitTransform()
    {
        if (_imageSize.Width <= 0 || _imageSize.Height <= 0) return;
        if (_containerSize.Width <= 0 || _containerSize.Height <= 0) return;

        double imageWidth = _imageSize.Width;
        double imageHeight = _imageSize.Height;
        double containerWidth = _containerSize.Width;
        double containerHeight = _containerSize.Height;

        double scale = Math.Min(containerWidth / imageWidth, containerHeight / imageHeight);

        double scaledWidth = imageWidth * scale;
        double scaledHeight = imageHeight * scale;
        double translateX = (containerWidth - scaledWidth) / 2;
        double translateY = (containerHeight - scaledHeight) / 2;

        TransformMatrix = Matrix.CreateScale(scale, scale) *
                        Matrix.CreateTranslation(translateX, translateY);

        CurrentFitScale = scale;
        UpdateZoomPercent();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>将视野居中到指定归一化坐标的标签</summary>
    public void CenterOnLabel(double normalizedX, double normalizedY)
    {
        if (_imageSize.Width <= 0 || _imageSize.Height <= 0) return;

        double labelX = normalizedX * _imageSize.Width;
        double labelY = normalizedY * _imageSize.Height;

        double centerX = _containerSize.Width / 2;
        double centerY = _containerSize.Height / 2;

        double currentScale = GetCurrentScale();
        double translateX = centerX - labelX * currentScale;
        double translateY = centerY - labelY * currentScale;

        TransformMatrix = new Matrix(
            TransformMatrix.M11, TransformMatrix.M12,
            TransformMatrix.M21, TransformMatrix.M22,
            translateX, translateY);

        TransformMatrix = ApplyCentering(TransformMatrix);
        UpdateZoomPercent();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>重置变换矩阵为 Identity</summary>
    public void ResetTransform()
    {
        TransformMatrix = Matrix.Identity;
        _isPanning = false;
        UpdateZoomPercent();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>容器尺寸变化时重新应用边界限制</summary>
    public void OnContainerSizeChanged()
    {
        if (_imageSize.Width <= 0 || _imageSize.Height <= 0) return;
        TransformMatrix = ApplyCentering(TransformMatrix);
        UpdateZoomPercent();
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    // ========================
    // 事件
    // ========================

    /// <summary>变换矩阵变更事件（code-behind 监听以同步到 UI）</summary>
    public event EventHandler? TransformChanged;

    // ========================
    // 内部方法（纯数学）
    // ========================

    /// <summary>
    /// 以指定点为中心进行缩放
    /// </summary>
    private Matrix ApplyZoom(Matrix matrix, double zoomFactor, Point centerPoint)
    {
        var zoomMatrix = Matrix.CreateTranslation(-centerPoint.X, -centerPoint.Y) *
                        Matrix.CreateScale(zoomFactor, zoomFactor) *
                        Matrix.CreateTranslation(centerPoint.X, centerPoint.Y);
        return matrix * zoomMatrix;
    }

    /// <summary>
    /// 自动居中：如果图片比视野小，则居中显示；否则限制边缘不能拖出视口
    /// </summary>
    private Matrix ApplyCentering(Matrix matrix)
    {
        if (_imageSize.Width <= 0 || _imageSize.Height <= 0) return matrix;
        if (_containerSize.Width <= 0 || _containerSize.Height <= 0) return matrix;

        var scaledSize = GetScaledImageSize(matrix);
        double scaledWidth = scaledSize.Width;
        double scaledHeight = scaledSize.Height;

        double translateX = matrix.M31;
        double translateY = matrix.M32;

        // --- X轴边界限制 ---
        double minX, maxX;
        if (scaledWidth < _containerSize.Width)
        {
            minX = 0;
            maxX = _containerSize.Width - scaledWidth;
        }
        else
        {
            minX = _containerSize.Width - scaledWidth;
            maxX = 0;
        }
        translateX = Math.Clamp(translateX, minX, maxX);

        // --- Y轴边界限制 ---
        double minY, maxY;
        if (scaledHeight < _containerSize.Height)
        {
            minY = 0;
            maxY = _containerSize.Height - scaledHeight;
        }
        else
        {
            minY = _containerSize.Height - scaledHeight;
            maxY = 0;
        }
        translateY = Math.Clamp(translateY, minY, maxY);

        return new Matrix(
            matrix.M11, matrix.M12,
            matrix.M21, matrix.M22,
            translateX, translateY);
    }

    /// <summary>
    /// 获取缩放后的图片尺寸
    /// </summary>
    private Size GetScaledImageSize(Matrix matrix)
    {
        if (_imageSize.Width <= 0 || _imageSize.Height <= 0) return new Size(0, 0);

        double scaleX = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        double scaleY = Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);

        return new Size(
            _imageSize.Width * scaleX,
            _imageSize.Height * scaleY);
    }

    /// <summary>
    /// 获取当前缩放比例
    /// </summary>
    private double GetCurrentScale()
    {
        return Math.Sqrt(TransformMatrix.M11 * TransformMatrix.M11 + TransformMatrix.M12 * TransformMatrix.M12);
    }

    /// <summary>
    /// 更新缩放百分比属性
    /// </summary>
    private void UpdateZoomPercent()
    {
        ZoomPercent = GetCurrentScale() * 100;
    }
}
