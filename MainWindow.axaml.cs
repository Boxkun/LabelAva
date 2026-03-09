using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Layout;
using LabelAva.Services;
using LabelAva.Models;
using LabelAva.Views;

namespace LabelAva;



public partial class MainWindow : Window
{
    // 当前图片
    private Bitmap? _currentImage;
    private string? _currentImagePath;
    
    // 翻译数据
    private TranslationData? _translationData;
    private string? _imageFolderPath;
    private int _currentImageIndex = 0;
    private List<string> _imageNames = new();
    
    // 矩阵变换
    private Matrix _transformMatrix = Matrix.Identity;
    private MatrixTransform? _matrixTransform;
    
    // 树视图数据
    private ObservableCollection<ImageTreeItem> _treeItems = new();
    
    // 记录上一次焦点的根节点（用于键盘导航时收起旧节点）
    private ImageTreeItem? _lastFocusedRootItem;
    
    // 当前图片对应的树视图项（用于保存和读取 FitScale）
    private ImageTreeItem? _currentTreeItem;
    
    // 拖动状态
    private bool _isPanning = false;
    private Point _lastPanPoint = new Point(0, 0);
    
    // 快捷键设置
    private ShortcutSettings _shortcutSettings;
    
    // 首次加载标志
    private bool _isFirstImageLoaded = false;
    
    // 标注控件列表
    private List<Control> _labelControls = new();
    
    // 状态栏
    private TextBlock? _statusText;
    private TextBlock? _zoomText;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // 加载快捷键设置
        _shortcutSettings = ShortcutSettingsService.Load();
        
        // 订阅快捷键设置更改事件
        PreferencesWindow.SettingsChanged += OnShortcutSettingsChanged;
        
        // 获取状态栏控件引用
        _statusText = this.FindControl<TextBlock>("_StatusText");
        _zoomText = this.FindControl<TextBlock>("_ZoomText");
        
        // 初始状态
        if (_statusText != null)
            _statusText.Text = "就绪";
        if (_zoomText != null)
            _zoomText.Text = "缩放: 100%";
        
        // 获取 MatrixTransform 引用（应用于包装 Grid）
        _matrixTransform = ImageWrapper.RenderTransform as MatrixTransform;
        if (_matrixTransform == null)
        {
            _matrixTransform = new MatrixTransform();
            ImageWrapper.RenderTransform = _matrixTransform;
        }
        
        // 绑定树视图
        ImageTreeView.ItemsSource = _treeItems;
        
        // 订阅容器尺寸变化事件
        ImageContainer.SizeChanged += OnImageContainerSizeChanged;
        
        // 订阅窗口关闭事件，确保清理资源
        this.Closing += OnWindowClosing;
    }
    
    /// <summary>
    /// 处理快捷键设置更改事件
    /// </summary>
    private void OnShortcutSettingsChanged(object? sender, ShortcutSettings settings)
    {
        _shortcutSettings = settings;
        UpdateStatus("快捷键设置已更新");
    }
    
    /// <summary>
    /// 窗口关闭时清理所有资源
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // 取消订阅事件
        ImageContainer.SizeChanged -= OnImageContainerSizeChanged;
        this.Closing -= OnWindowClosing;
        
        // 释放图片资源
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        
        // 清除标注控件
        foreach (var control in _labelControls)
        {
            ImageWrapper.Children.Remove(control);
        }
        _labelControls.Clear();
        
        // 清空树视图数据
        _treeItems.Clear();
        ImageTreeView.ItemsSource = null;
        
        // 清空其他数据
        _translationData = null;
        _imageFolderPath = null;
        _imageNames.Clear();
        
        // 重置状态
        _isFirstImageLoaded = false;
        _isPanning = false;
    }
    
    /// <summary>
    /// 图像容器尺寸变化时重新应用边界限制
    /// </summary>
    private void OnImageContainerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_currentImage == null) return;
        
        // 重新应用边界限制（居中/防越界）
        _transformMatrix = ApplyCentering(_transformMatrix);
        ApplyTransform();
        UpdateZoomText();
    }
    
    /// <summary>
    /// 应用变换矩阵到 Image 控件
    /// </summary>
    private void ApplyTransform()
    {
        if (_matrixTransform != null)
        {
            _matrixTransform.Matrix = _transformMatrix;
        }
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
        if (_treeItems.Count == 0) return;

        // 展开第一个项（如果需要）
        _treeItems[0].IsExpanded = true;

        // 选中第一个项
        ImageTreeView.SelectedItem = _treeItems[0];

        // 等待布局，再获取容器并设置焦点
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var container = ImageTreeView.ContainerFromItem(_treeItems[0]);
            if (container != null)
            {
                (container as Control)?.Focus();
            }
            else
            {
                // 如果容器未准备好，退回到 TreeView 聚焦
                ImageTreeView.Focus();
            }
        }, DispatcherPriority.Background);
    }
    
    // ==================== 菜单事件处理 ====================
    
    /// <summary>
    /// 处理来自 WelcomeView 的打开翻译请求
    /// </summary>
    private async void OnOpenTranslationRequested(object? sender, RoutedEventArgs e)
    {
        await OpenTranslationFileAsync();
    }
    
    private async void OnOpenTranslationFile(object? sender, RoutedEventArgs e)
    {
        await OpenTranslationFileAsync();
    }

    /// <summary>
    /// 打开翻译文件的核心逻辑
    /// </summary>
    private async Task OpenTranslationFileAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择翻译文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
            }
        });
        
        if (files.Count > 0)
        {
            var filePath = files[0].Path.LocalPath;
            
            // 解析翻译文件
            var parser = new TranslationParser();
            _translationData = parser.Parse(filePath);
            
            // 获取翻译文件所在目录，作为图片文件夹
            _imageFolderPath = Path.GetDirectoryName(filePath);
            
            // 获取所有图片名
            _imageNames = new List<string>(_translationData.ImageLabels.Keys);
            
            if (_imageNames.Count > 0)
            {
                _currentImageIndex = 0;
                LoadCurrentImage();
                BuildTreeView();
                UpdateStatus($"已加载 {_imageNames.Count} 张图片");
                
                // 切换到主界面
                ShowMainContent();
                
                // 加载完成后，将焦点设置到右侧树状视图
                // 使用 Task.Delay 延迟设置焦点，确保菜单已完全关闭
                _ = SetFocusAfterDelayAsync();
            }
            else
            {
                UpdateStatus("解析翻译文件失败");
            }
        }
    }
    
    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void OnPreferences(object? sender, RoutedEventArgs e)
    {
        var preferencesWindow = new Views.PreferencesWindow();
        preferencesWindow.Show();
    }
    
    /// <summary>
    /// 关闭翻译文件，回到欢迎屏幕
    /// </summary>
    private void OnCloseTranslation(object? sender, RoutedEventArgs e)
    {
        // 清空图片
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        _currentImagePath = null;
        _transformMatrix = Matrix.Identity;
        ApplyTransform();
        
        // 清除标注
        foreach (var control in _labelControls)
        {
            ImageWrapper.Children.Remove(control);
        }
        _labelControls.Clear();
        
        // 清空数据
        _translationData = null;
        _imageFolderPath = null;
        _imageNames.Clear();
        _treeItems.Clear();
        _isFirstImageLoaded = false;
        
        // 切换回欢迎屏幕
        ShowWelcomeScreen();
        
        UpdateStatus("就绪");
    }
    
    /// <summary>
    /// 显示主界面，隐藏欢迎屏幕
    /// </summary>
    private void ShowMainContent()
    {
        WelcomeViewControl.IsVisible = false;
        MainContentPanel.IsVisible = true;
        CloseTranslationMenuItem.IsEnabled = true;
    }
    
    /// <summary>
    /// 显示欢迎屏幕，隐藏主界面
    /// </summary>
    private void ShowWelcomeScreen()
    {
        WelcomeViewControl.IsVisible = true;
        MainContentPanel.IsVisible = false;
        CloseTranslationMenuItem.IsEnabled = false;
    }
    
    private void OnAddLabel(object? sender, RoutedEventArgs e)
    {
        UpdateStatus("添加标注模式");
    }
    
    private void OnDeleteLabel(object? sender, RoutedEventArgs e)
    {
        UpdateStatus("删除标注模式");
    }
    
    private void OnClearCanvas(object? sender, RoutedEventArgs e)
    {
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        _currentImagePath = null;
        _transformMatrix = Matrix.Identity;
        ApplyTransform();
        
        // 清除标注
        foreach (var control in _labelControls)
        {
            ImageWrapper.Children.Remove(control);
        }
        _labelControls.Clear();
        
        UpdateStatus("画布已清空");
    }
    
    private void OnZoomIn(object? sender, RoutedEventArgs e)
    {
        if (_currentImage == null) return;
        
        // 以容器中心为基准进行缩放
        var containerBounds = ImageContainer.Bounds;
        if (containerBounds.Width <= 0 || containerBounds.Height <= 0)
        {
            // 容器未准备好，等待下次
            return;
        }
        
        var centerPoint = new Point(containerBounds.Width / 2, containerBounds.Height / 2);
        
        _transformMatrix = ApplyZoom(_transformMatrix, 1.2, centerPoint);
        ApplyTransform();
        
        UpdateZoomText();
    }
    
    private void OnZoomOut(object? sender, RoutedEventArgs e)
    {
        if (_currentImage == null) return;
        
        // 以容器中心为基准进行缩放
        var containerBounds = ImageContainer.Bounds;
        if (containerBounds.Width <= 0 || containerBounds.Height <= 0)
        {
            // 容器未准备好，等待下次
            return;
        }
        
        var centerPoint = new Point(containerBounds.Width / 2, containerBounds.Height / 2);
        
        _transformMatrix = ApplyZoom(_transformMatrix, 0.9, centerPoint);
        ApplyTransform();
        
        UpdateZoomText();
    }
    
    private void OnResetZoom(object? sender, RoutedEventArgs e)
    {
        if (_currentImage == null) return;
        
        // 重置为自适应状态
        CalculateFitTransform();
        ApplyTransform();
        UpdateZoomText();
    }
    
    private void OnAbout(object? sender, RoutedEventArgs e)
    {
        UpdateStatus("LabelAva 1.0");
    }
    
    // ==================== ImageContainer 事件处理 ====================
    
    private void OnImageContainerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(ImageContainer);
        
        // 左键拖动
        if (point.Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = e.GetPosition(ImageContainer);
            ImageContainer.Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
    }
    
    private void OnImageContainerPointerMoved(object? sender, PointerEventArgs e)
{
    if (_isPanning)
    {
        var currentPoint = e.GetPosition(ImageContainer);
        var delta = currentPoint - _lastPanPoint;
        
        _transformMatrix = new Matrix(
            _transformMatrix.M11, _transformMatrix.M12,
            _transformMatrix.M21, _transformMatrix.M22,
            _transformMatrix.M31 + delta.X, _transformMatrix.M32 + delta.Y);
        
        _transformMatrix = ApplyCentering(_transformMatrix);
        
        ApplyTransform();
        
        _lastPanPoint = currentPoint;
    }
}
    
    private void OnImageContainerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ImageContainer.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }
    
    private void OnImageContainerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_currentImage == null) return;
        
        // 获取鼠标在容器中的位置
        var mousePos = e.GetPosition(ImageContainer);
        
        // 确定缩放因子
        double zoom = e.Delta.Y > 0 ? 1.1 : 0.9;
        
        // 以鼠标为中心进行缩放
        _transformMatrix = ApplyZoom(_transformMatrix, zoom, mousePos);
        
        // 自动居中：如果图片缩小后小于容器
        _transformMatrix = ApplyCentering(_transformMatrix);
        
        ApplyTransform();
        
        UpdateZoomText();
        e.Handled = true;
    }
    
    // ==================== 矩阵变换核心方法 ====================
    
    /// <summary>
    /// 以指定点为中心进行缩放
    /// </summary>
    private Matrix ApplyZoom(Matrix matrix, double zoomFactor, Point centerPoint)
    {
        // 【修复】标准的以某点为中心的缩放矩阵乘法组合
        var zoomMatrix = Matrix.CreateTranslation(-centerPoint.X, -centerPoint.Y) *
                        Matrix.CreateScale(zoomFactor, zoomFactor) *
                        Matrix.CreateTranslation(centerPoint.X, centerPoint.Y);
        
        // 将缩放效果叠加到当前矩阵上
        return matrix * zoomMatrix;
    }
    
    /// <summary>
    /// 自动居中：如果图片比视野小，则居中显示
    /// </summary>
    private Matrix ApplyCentering(Matrix matrix)
    {
        if (_currentImage == null) return matrix;
        
        var containerBounds = ImageContainer.Bounds;
        if (containerBounds.Width <= 0 || containerBounds.Height <= 0)
            return matrix;
        
        var scaledSize = GetScaledImageSize();
        double scaledWidth = scaledSize.Width;
        double scaledHeight = scaledSize.Height;
        
        double translateX = matrix.M31;
        double translateY = matrix.M32;
        
        // --- X轴边界限制 ---
        double minX, maxX;
        if (scaledWidth < containerBounds.Width)
        {
            // 图片比容器窄：限制在容器内部，不允许被拖出去
            minX = 0;
            maxX = containerBounds.Width - scaledWidth;
        }
        else
        {
            // 图片比容器宽：限制边缘不能拖出视口（撞墙）
            minX = containerBounds.Width - scaledWidth;
            maxX = 0;
        }
        translateX = Math.Clamp(translateX, minX, maxX);
        
        // --- Y轴边界限制 ---
        double minY, maxY;
        if (scaledHeight < containerBounds.Height)
        {
            // 图片比容器矮：限制在容器内部
            minY = 0;
            maxY = containerBounds.Height - scaledHeight;
        }
        else
        {
            // 图片比容器高：限制边缘不能拖出视口
            minY = containerBounds.Height - scaledHeight;
            maxY = 0;
        }
        translateY = Math.Clamp(translateY, minY, maxY);
        
        // 返回应用了边界限制的新矩阵
        return new Matrix(
            matrix.M11, matrix.M12,
            matrix.M21, matrix.M22,
            translateX, translateY);
    }
    
    /// <summary>
    /// 获取缩放后的图片尺寸
    /// </summary>
    private Size GetScaledImageSize()
    {
        if (_currentImage == null) return new Size(0, 0);
        
        // 从矩阵中提取缩放因子
        double scaleX = Math.Sqrt(_transformMatrix.M11 * _transformMatrix.M11 + _transformMatrix.M12 * _transformMatrix.M12);
        double scaleY = Math.Sqrt(_transformMatrix.M21 * _transformMatrix.M21 + _transformMatrix.M22 * _transformMatrix.M22);
        
        return new Size(
            _currentImage.Size.Width * scaleX,
            _currentImage.Size.Height * scaleY
        );
    }
    
    /// <summary>
    /// 计算适应容器的初始变换（Fit模式）
    /// </summary>
    private void CalculateFitTransform()
    {
        if (_currentImage == null) return;
        
        var containerBounds = ImageContainer.Bounds;
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
        
        double imageWidth = _currentImage.Size.Width;
        double imageHeight = _currentImage.Size.Height;
        double containerWidth = containerBounds.Width;
        double containerHeight = containerBounds.Height;
        
        // 计算自适应缩放比例
        double scale = Math.Min(containerWidth / imageWidth, containerHeight / imageHeight);
        
        // 计算居中偏移
        double scaledWidth = imageWidth * scale;
        double scaledHeight = imageHeight * scale;
        double translateX = (containerWidth - scaledWidth) / 2;
        double translateY = (containerHeight - scaledHeight) / 2;
        
        // 【修复】一定要先 CreateScale，再乘以 CreateTranslation！
        _transformMatrix = Matrix.CreateScale(scale, scale) * 
                        Matrix.CreateTranslation(translateX, translateY);
        
        // 保存当前图片的 fit 缩放比例
        SaveCurrentFitScale(scale);
    }
    
    /// <summary>
    /// 保存当前图片的 fit 缩放比例到对应的树视图项
    /// </summary>
    private void SaveCurrentFitScale(double fitScale)
    {
        if (_currentImagePath == null || _treeItems.Count == 0) return;
        
        string imageName = Path.GetFileName(_currentImagePath);
        
        // 找到对应的 ImageTreeItem 并保存 fit 比例
        foreach (var item in _treeItems)
        {
            if (item.ImageName == imageName)
            {
                item.FitScale = fitScale;
                _currentTreeItem = item;
                break;
            }
        }
    }
    
    /// <summary>
    /// 获取当前缩放比例
    /// </summary>
    private double GetCurrentScale()
    {
        return Math.Sqrt(_transformMatrix.M11 * _transformMatrix.M11 + _transformMatrix.M12 * _transformMatrix.M12);
    }
    
    // ==================== 图片加载 ====================

    /// <summary>
    /// 更新标注：根据当前图片的标注数据，在画布上显示编号
    /// </summary>
    private void UpdateLabels()
    {
        // 移除旧的标注
        foreach (var control in _labelControls)
        {
            ImageWrapper.Children.Remove(control);
        }
        _labelControls.Clear();

        // 没有图片或翻译数据时返回
        if (_currentImage == null || _translationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;

        // 获取当前图片的文件名（作为键）
        string imageName = Path.GetFileName(_currentImagePath);
        if (!_translationData.ImageLabels.TryGetValue(imageName, out var labels))
            return;

        double imageWidth = _currentImage.Size.Width;
        double imageHeight = _currentImage.Size.Height;

        foreach (var label in labels)
        {
            var border = new Border
            {
                Width = 64,
                Height = 64,
                Background = new SolidColorBrush(Colors.Coral, 0.8),
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = label.TextIndex, // 将编号作为标识存储，用于后续高亮匹配
                Child = new TextBlock
                {
                    Text = label.TextIndex.ToString(),   // 显示编号
                    Foreground = Brushes.White,
                    FontSize = 48,
                    FontFamily = new FontFamily("Sarasa Mono SC"),
                    FontWeight = FontWeight.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            
            // 设置归一化坐标对应的像素位置
            Canvas.SetLeft(border, label.X * imageWidth - 32);  // 减去一半宽度以居中
            Canvas.SetTop(border, label.Y * imageHeight - 32);  // 减去一半高度以居中

            // 将标注置于顶层
            border.ZIndex = 10;

            // 添加到 ImageWrapper 内部，使其继承图片的变换
            ImageWrapper.Children.Add(border);
            _labelControls.Add(border);
        }
        
        // 如果当前恰好有选中的翻译文本子项，则在新生成的标注中自动高亮它
        if (ImageTreeView.SelectedItem is TranslationTreeItem selectedTranslation)
        {
            HighlightLabel(selectedTranslation.Index);
        }
    }
    
    /// <summary>
    /// 高亮显示指定编号的标注控件
    /// </summary>
    private void HighlightLabel(int labelIndex)
    {
        foreach (var control in _labelControls)
        {
            if (control is Border border && border.Tag is int index)
            {
                if (index == labelIndex)
                {
                    // 高亮状态：蓝色背景，加粗白边框，并提升图层显示层级
                    border.Background = new SolidColorBrush(Colors.DodgerBlue, 0.9);
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(3);
                    border.ZIndex = 20; 
                }
                else
                {
                    // 普通状态：恢复默认的珊瑚色与样式
                    border.Background = new SolidColorBrush(Colors.Coral, 0.8);
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                    border.ZIndex = 10;
                }
            }
        }
    }
    
    private void LoadCurrentImage()
    {
        if (_imageNames.Count == 0 || string.IsNullOrEmpty(_imageFolderPath))
            return;
        
        if (_currentImageIndex < 0 || _currentImageIndex >= _imageNames.Count)
            return;
        
        var imageName = _imageNames[_currentImageIndex];
        var imagePath = Path.Combine(_imageFolderPath!, imageName);
        
        if (File.Exists(imagePath))
        {
            LoadImage(imagePath);
        }
        else
        {
            UpdateStatus($"图片文件不存在: {imagePath}");
        }
    }
    
    private void LoadImage(string imagePath)
    {
        try
        {
            if (_currentImage != null)
            {
                _currentImage.Dispose();
            }
            
            // 首次加载时先隐藏图片，避免居中完成前显示导致闪烁
            // 后续切换直接覆盖旧图片，避免闪烁
            if (!_isFirstImageLoaded)
            {
                MainImage.IsVisible = false;
            }
            
            _currentImage = new Bitmap(imagePath);
            _currentImagePath = imagePath;
            
            // 设置图片源
            MainImage.Source = _currentImage;
            
            // 找到当前图片对应的树视图项
            string imageName = Path.GetFileName(imagePath);
            foreach (var item in _treeItems)
            {
                if (item.ImageName == imageName)
                {
                    _currentTreeItem = item;
                    break;
                }
            }
            
            // 首次加载时延迟计算适应容器的初始变换，等待布局完成
            if (!_isFirstImageLoaded)
            {
                _isFirstImageLoaded = true;
                
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // 等待布局完成
                    
                    // 在 UI 线程上执行
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        CalculateFitTransform();
                        ApplyTransform();
                        UpdateZoomText();

                        // 居中完成后显示图片
                        MainImage.IsVisible = true;
                        
                        // 更新标注显示
                        UpdateLabels();
                    });
                });
            }
            else
            {
                // 非首次加载直接应用已有的变换
                ApplyTransform();
                UpdateZoomText();
                // 更新标注显示
                UpdateLabels();
            }
            
            // 更新状态栏显示当前图片信息
            if (_imageNames.Count > 0)
            {
                UpdateStatus($"[{_currentImageIndex + 1}/{_imageNames.Count}] {Path.GetFileName(imagePath)}");
            }
            else
            {
                UpdateStatus($"已加载图片: {Path.GetFileName(imagePath)}");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"加载图片失败: {ex.Message}");
            // 发生异常时也要确保图片可见
            MainImage.IsVisible = true;
        }
    }
    
    // ==================== 辅助方法 ====================
    
    private void UpdateZoomText()
    {
        // 从矩阵中提取缩放比例
        double scaleX = Math.Sqrt(_transformMatrix.M11 * _transformMatrix.M11 + _transformMatrix.M12 * _transformMatrix.M12);
        if (_zoomText != null)
            _zoomText.Text = $"缩放: {scaleX * 100:F0}%";
    }
    
    private void UpdateStatus(string message)
    {
        if (_statusText != null)
            _statusText.Text = message;
    }
    
    /// <summary>
    /// 将文本复制到系统剪贴板
    /// </summary>
    private async void CopyToClipboard(string text)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.Clipboard == null) return;
        
        await topLevel.Clipboard.SetTextAsync(text);
    }
    
    // ==================== 树视图相关方法 ====================
    
    /// <summary>
    /// 构建树视图数据
    /// </summary>
    private void BuildTreeView()
    {
        _treeItems.Clear();
        
        if (_translationData == null) return;
        
        bool isFirstItem = true; // 标记是否为第一个元素
        
        foreach (var kvp in _translationData.ImageLabels)
        {
            var imageItem = new ImageTreeItem
            {
                ImageName = kvp.Key,
                IsExpanded = isFirstItem // 初次加载时，只有第一个元素设为 true
            };
            
            isFirstItem = false;
            
            foreach (var label in kvp.Value)
            {
                imageItem.Translations.Add(new TranslationTreeItem
                {
                    Index = label.TextIndex,
                    Text = label.Text
                });
            }
            
            _treeItems.Add(imageItem);
        }
    }
    
    /// <summary>
    /// 树视图选择变更事件（处理图片切换 & 自动折叠展开）
    /// </summary>
    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedItem = ImageTreeView.SelectedItem;
        if (selectedItem == null) return;

        ImageTreeItem? targetRootItem = null;

        // 1. 判断选中项是父节点(图片)还是子节点(翻译文本)
        if (selectedItem is ImageTreeItem rootItem)
        {
            targetRootItem = rootItem;
            
            // 切换图片
            var index = _imageNames.IndexOf(rootItem.ImageName);
            if (index >= 0 && _currentImageIndex != index)
            {
                _currentImageIndex = index;
                LoadCurrentImage();
                CalculateFitTransform();
                ApplyTransform();
                UpdateZoomText();
                UpdateLabels();
            }
        }
        else if (selectedItem is TranslationTreeItem childItem)
        {
            // 如果通过方向键选到了子节点，我们需要找到它对应的父节点
            foreach (var root in _treeItems)
            {
                if (root.Translations.Contains(childItem))
                {
                    targetRootItem = root;
                    break;
                }
            }
            
            // 如果子节点对应的图片还没加载，则切换图片
            if (targetRootItem != null)
            {
                var index = _imageNames.IndexOf(targetRootItem.ImageName);
                if (index >= 0 && _currentImageIndex != index)
                {
                    _currentImageIndex = index;
                    LoadCurrentImage();
                    CalculateFitTransform();
                    ApplyTransform();
                    UpdateZoomText();
                    UpdateLabels();
                }
            }
        }

        // 2. 动态展开/收起逻辑 (手风琴效果)
        if (targetRootItem != null)
        {
            foreach (var item in _treeItems)
            {
                // 如果是当前焦点所在的父节点，展开它；其他的全部收起
                item.IsExpanded = (item == targetRootItem);
            }
            // 更新上一次焦点的根节点
            _lastFocusedRootItem = targetRootItem;
        }
        
        // 3. 高亮与视野居中处理
        if (selectedItem is TranslationTreeItem targetChildItem)
        {
            // 切换图片视图中的编号高亮
            HighlightLabel(targetChildItem.Index);
            
            double currentScale = GetCurrentScale();
            double fitScale = targetRootItem?.FitScale ?? 1.0;
            
            // 如果当前缩放比例大于 fit 比例（用户手动放大了）
            if (currentScale > fitScale)
            {
                CenterOnLabel(targetChildItem.Index);
            }
        }
        else if (selectedItem is ImageTreeItem)
        {
            // 如果选中的是图片本身（根节点），则取消所有编号的高亮
            HighlightLabel(-1);
        }
    }
    
    /// <summary>
    /// 树视图键盘导航处理
    /// </summary>
    private void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        var selectedItem = ImageTreeView.SelectedItem;
        if (selectedItem == null) return;

        // 创建当前按键的 KeyGesture 用于比较
        var currentGesture = new KeyGesture(e.Key, e.KeyModifiers);

        // 检查是否匹配复制快捷键（使用自定义快捷键设置）
        if (_shortcutSettings.CopyText != null && 
            currentGesture.Equals(_shortcutSettings.CopyText))
        {
            if (selectedItem is TranslationTreeItem translationItem)
            {
                CopyToClipboard(translationItem.Text);
                UpdateStatus($"已复制: {translationItem.Text}");
            }
            else if (selectedItem is ImageTreeItem imageItem)
            {
                //CopyToClipboard(imageItem.ImageName);
                //UpdateStatus($"已复制: {imageItem.ImageName}");
            }
            e.Handled = true;
            return;
        }

        // 记录焦点变化前的根节点
        ImageTreeItem? oldRootItem = null;
        if (selectedItem is ImageTreeItem)
        {
            oldRootItem = selectedItem as ImageTreeItem;
        }
        else if (selectedItem is TranslationTreeItem currentChildItem)
        {
            // 找到当前子节点对应的父节点
            foreach (var root in _treeItems)
            {
                if (root.Translations.Contains(currentChildItem))
                {
                    oldRootItem = root;
                    break;
                }
            }
        }

        // 检查是否匹配自定义的上/下导航快捷键（主要或次要）
        bool isNavigateUp = false;
        bool isNavigateDown = false;
        
        // 检查上导航（主要或次要）
        if (_shortcutSettings.NavigateUp != null && 
            currentGesture.Equals(_shortcutSettings.NavigateUp))
        {
            isNavigateUp = true;
        }
        else if (_shortcutSettings.NavigateUpSecondary != null && 
                 currentGesture.Equals(_shortcutSettings.NavigateUpSecondary))
        {
            isNavigateUp = true;
        }
        
        // 检查下导航（主要或次要）
        if (_shortcutSettings.NavigateDown != null && 
            currentGesture.Equals(_shortcutSettings.NavigateDown))
        {
            isNavigateDown = true;
        }
        else if (_shortcutSettings.NavigateDownSecondary != null && 
                 currentGesture.Equals(_shortcutSettings.NavigateDownSecondary))
        {
            isNavigateDown = true;
        }
        
        // 如果匹配自定义导航快捷键，手动执行选中项切换
        if (isNavigateUp || isNavigateDown)
        {
            var visibleItems = new List<object>();
            foreach (var root in _treeItems)
            {
                visibleItems.Add(root);
                if (root.IsExpanded)
                {
                    foreach (var child in root.Translations)
                        visibleItems.Add(child);
                }
            }
            
            int currentIndex = visibleItems.IndexOf(selectedItem);
            if (currentIndex >= 0)
            {
                if (isNavigateUp && currentIndex > 0)
                {
                    ImageTreeView.SelectedItem = visibleItems[currentIndex - 1];
                    e.Handled = true;
                }
                else if (isNavigateDown && currentIndex < visibleItems.Count - 1)
                {
                    ImageTreeView.SelectedItem = visibleItems[currentIndex + 1];
                    e.Handled = true;
                }
            }
        }

        // 方向键处理：展开新焦点子项，收起旧焦点子项
        if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Right ||
            isNavigateUp || isNavigateDown)
        {
            // 延迟执行，等待SelectionChanged事件完成
            Dispatcher.UIThread.Post(() =>
            {
                var newSelectedItem = ImageTreeView.SelectedItem;
                if (newSelectedItem == null) return;

                ImageTreeItem? newRootItem = null;
                if (newSelectedItem is ImageTreeItem)
                {
                    newRootItem = newSelectedItem as ImageTreeItem;
                }
                else if (newSelectedItem is TranslationTreeItem newChildItem)
                {
                    foreach (var root in _treeItems)
                    {
                        if (root.Translations.Contains(newChildItem))
                        {
                            newRootItem = root;
                            break;
                        }
                    }
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
                    _lastFocusedRootItem = newRootItem;
                }
                
                // 确保新选中的项获得焦点，触发视图滚动
                var container = ImageTreeView.ContainerFromItem(newSelectedItem) as Control;
                container?.Focus();
            }, DispatcherPriority.Background);
        }
    }
    
    /// <summary>
    /// 将视野中心对准指定编号的标注
    /// </summary>
    private void CenterOnLabel(int labelIndex)
    {
        if (_currentImage == null || _translationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;
        
        // 获取当前图片的标注数据
        string imageName = Path.GetFileName(_currentImagePath);
        if (!_translationData.ImageLabels.TryGetValue(imageName, out var labels))
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
        
        // 计算标注在图片上的像素坐标
        double imageWidth = _currentImage.Size.Width;
        double imageHeight = _currentImage.Size.Height;
        double labelX = targetLabel.X * imageWidth;
        double labelY = targetLabel.Y * imageHeight;
        
        // 获取容器中心（目标视野中心）
        var containerBounds = ImageContainer.Bounds;
        double centerX = containerBounds.Width / 2;
        double centerY = containerBounds.Height / 2;
        
        // 计算需要的平移量：将标注点从 (labelX, labelY) 移动到 (centerX, centerY)
        // 变换公式：屏幕坐标 = 图片坐标 * scale + translate
        // 所以：translate = 屏幕坐标 - 图片坐标 * scale
        double currentScale = GetCurrentScale();
        double translateX = centerX - labelX * currentScale;
        double translateY = centerY - labelY * currentScale;
        
        // 应用变换（保持缩放比例不变，只调整位置）
        _transformMatrix = new Matrix(
            _transformMatrix.M11, _transformMatrix.M12,
            _transformMatrix.M21, _transformMatrix.M22,
            translateX, translateY);
        
        // 应用边界限制
        _transformMatrix = ApplyCentering(_transformMatrix);
        
        ApplyTransform();
    }
}
