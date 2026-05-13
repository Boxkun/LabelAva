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


namespace LabelAva;



public partial class MainWindow : Window
{
    // 快捷键设置
    private readonly AppSettingsProvider _settingsProvider = new();
    private ShortcutRouter _shortcutRouter = null!;
    
    // 编辑模式相关
    private TextBox? _translationTextBox;
    private Border? _editPanel;
    
    // UI 锁：防止命令执行时触发 UI 事件污染历史栈
    private bool _isUpdatingUI = false;
    
    // 树视图拖拽交互状态
    private Point _treeDragStartPoint;
    private bool _isTreeItemDragging = false;   // PENDING 状态：按下但未超过阈值
    private bool _isDragActive = false;          // DRAGGING 状态：正在拖拽中
    private TranslationTreeItem? _draggedTreeItem;
    private TranslationTreeItem? _currentDropTarget; // 当前放置目标
    private TreeView? _imageTreeView;           // TreeView 引用缓存
    
    // 选中项同步防重入标志
    private bool _isSyncingSelection = false;
    
    // 选中项来源标志：true 表示选中变更由画布交互触发，需跳过视野居中
    private bool _isSelectionFromCanvas = false;

    // 异步初始化标志：防止初始化完成前的空引用
    private bool _isInitialized = false;

    // 分组单选按钮（Avalonia 自动生成 x:Name 字段）
    
    public MainWindowViewModel ViewModel => ((MainWindowViewModel)DataContext!);
    public StatusBarViewModel StatusBar => ViewModel.StatusBar;
    public EditViewModel Edit => ViewModel.Edit;
    public DocumentViewModel Document => ViewModel.Document;
    public NavigationViewModel Navigation => ViewModel.Navigation;
    public CanvasWorkspaceViewModel CanvasWorkspace => ViewModel.CanvasWorkspace;
    
    // ==================== AnnotationCanvas 便捷属性 ====================
    public AnnotationCanvas CanvasControl => this.FindControl<AnnotationCanvas>("AnnotationCanvasControl")!;

    // 内存追踪正常态窗口尺寸（最大化时用于保存恢复尺寸）
    private double _normalWidth;
    private double _normalHeight;
    private PixelPoint _normalPosition;
    private DispatcherTimer? _sizeDebounce;
    private bool _savedMaximized;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Resources["DligFontFamily"] = null;
        Resources["DligFontFeatures"] = null;

        // 恢复上次窗口尺寸/位置（不立即最大化，让 OS 先记录 Normal 尺寸）
        _settingsProvider.Load();
        var s = _settingsProvider.Current;
        _savedMaximized = s.WindowMaximized;
        if (s.WindowX >= 0 && s.WindowY >= 0)
        {
            var pos = new PixelPoint(s.WindowX, s.WindowY);
            if (IsPositionValid(pos, s.WindowWidth, s.WindowHeight))
            {
                Position = pos;
                Width = s.WindowWidth;
                Height = s.WindowHeight;
            }
        }

        // ===== 仅保留窗口级事件订阅（不依赖任何 VM） =====
        
        // 启用拖放
        DragDrop.SetAllowDrop(this, true);
        
        // 订阅窗口关闭事件，确保清理资源
        this.Closing += OnWindowClosing;
        
        // 订阅拖放事件
        this.AddHandler(DragDrop.DropEvent, OnFileDrop);
        this.AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        
        // 订阅鼠标按键事件（用于处理鼠标侧键快捷键）
        this.PointerPressed += OnMainWindowPointerPressed;
        
        // 注册全局快捷键隧道拦截，在控件捕获前优先接管撤销/重做
        // Ctrl+Enter 提交功能也在这里处理（兼容主键盘 Return 和数字小键盘 Enter）
        this.AddHandler(InputElement.KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
        
        // 异步初始化入口
        this.Opened += OnWindowFirstOpened;
    }
    
    /// <summary>
    /// 校验保存的窗口位置在当前屏幕配置下是否至少标题栏可见
    /// </summary>
    private bool IsPositionValid(PixelPoint pos, double w, double h)
    {
        if (Screens is null) return false;
        var titleArea = new PixelRect(pos, new PixelSize((int)w, 60));
        foreach (var screen in Screens.All)
        {
            if (screen.Bounds.Intersects(titleArea))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// 首次打开窗口时的处理：等待首帧渲染完成后显示窗口
    /// </summary>
    private async void OnWindowFirstOpened(object? sender, EventArgs e)
    {
        this.Opened -= OnWindowFirstOpened;

        // 等待首帧渲染完成（Render 优先级确保布局+渲染 pass 已执行）
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        // 延迟最大化：先以 Normal 尺寸呈现，让 OS 记录 Normal 尺寸后最大化
        if (_savedMaximized)
            WindowState = WindowState.Maximized;

        // 首帧已上屏，安全地显示窗口
        this.Opacity = 1;

        // 异步执行重工作
        await InitializeAsync();

        // 初始化正常态尺寸追踪（从 settings 读取，避免最大化状态下读到膨胀值）
        {
            var s = _settingsProvider.Current;
            _normalWidth = s.WindowWidth;
            _normalHeight = s.WindowHeight;
            _normalPosition = new PixelPoint(s.WindowX, s.WindowY);
        }

        _sizeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _sizeDebounce.Tick += (_, _) =>
        {
            _sizeDebounce.Stop();
            if (WindowState != WindowState.Normal)
            {
                System.Diagnostics.Debug.WriteLine($"[Debounce] SKIPPED, state={WindowState}");
                return;
            }
            _normalWidth = Width;
            _normalHeight = Height;
            _normalPosition = Position;
            System.Diagnostics.Debug.WriteLine($"[Debounce] saved normal: {Width}x{Height} @ {Position}");
        };

        this.PropertyChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                if (e.Property == WidthProperty || e.Property == HeightProperty)
                {
                    _sizeDebounce.Stop();
                    _sizeDebounce.Start();
                }
            }
        };
    }

    /// <summary>
    /// 异步初始化方法：在后台执行文件 I/O + VM 创建 + 事件订阅
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // ---- Phase 1: 文件 I/O 移到后台线程 ----
            await Task.Run(() => _settingsProvider.Load());
            _shortcutRouter = new ShortcutRouter(_settingsProvider.Current.Shortcuts);

            // ---- Phase 2: UI 线程上的轻量操作 ----
            UpdateGroupButtonsShortcutTips();
            _settingsProvider.SettingsChanged += OnSettingsChanged;

            StatusBar.UpdateStatus("就绪", StatusBarViewModel.StatusType.Info);
            StatusBar.UpdateZoom(100);

            _translationTextBox = this.FindControl<TextBox>("TranslationTextBox");
            _editPanel = this.FindControl<Border>("EditPanel");
            if (_translationTextBox != null)
                _translationTextBox.LostFocus += OnTranslationTextBoxLostFocus;

            _imageTreeView = this.FindControl<TreeView>("ImageTreeView")!;

            // ---- Phase 3: VM 创建（有依赖顺序） ----
            var historyManager = new HistoryManager();
            ViewModel.History = new HistoryViewModel(historyManager, CommitCurrentEdit, StatusBar);
            ViewModel.History.HistoryStateChanged += OnHistoryStateChanged;

            ViewModel.CanvasWorkspace = new CanvasWorkspaceViewModel(ViewModel.History, StatusBar, CommitCurrentEdit);
            ViewModel.CanvasWorkspace.TransformChanged += OnCanvasTransformChanged;

            ViewModel.Edit = new EditViewModel(StatusBar);
            ViewModel.Edit.EditModeChanged += OnEditModeChanged;
            ViewModel.Edit.GroupChanged += OnGroupChanged;

            ViewModel.Navigation = new NavigationViewModel(StatusBar);
            ViewModel.Navigation.CurrentImageChanged += OnNavigationCurrentImageChanged;
            ViewModel.Navigation.SelectedItemChanged += OnNavigationSelectedItemChanged;

            var fileService = new FileDialogService(() => GetTopLevel(this));
            ViewModel.Document = new DocumentViewModel(
                fileService, ViewModel.History, StatusBar,
                ShowUnsavedChangesDialogAsync, ShowImageSelectionDialogAsync,
                ShowImageAssociationDialogAsync
            );
            ViewModel.Document.DocumentOpened += OnDocumentOpened;
            ViewModel.Document.DocumentClosed += OnDocumentClosed;

            // ---- Phase 4: Canvas 初始化 ----
            CanvasControl.SettingsProvider = _settingsProvider;
            GroupIndexToBrushConverter.Initialize(_settingsProvider);
            CanvasControl.CommitCurrentEdit = CommitCurrentEdit;
            CanvasControl.SelectLabelByIndex = SelectLabelByIndex;
            CanvasControl.IsEditMode = Edit.IsEditMode;
            CanvasControl.LabelClicked += (_, labelIndex) =>
            {
                _isSelectionFromCanvas = true;
                SelectLabelByIndex(labelIndex);
            };
            CanvasControl.AddLabelRequested += OnCanvasAddLabelRequested;
            CanvasControl.LabelMoved += OnCanvasLabelMoved;

            // 初始化完成
            _isInitialized = true;

            // 应用连字配置
            DligConfigService.EnsureDirectory();
            ApplyDligConfig();
        }
        catch (Exception ex)
        {
            StatusBar.UpdateStatus($"初始化失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
        }
    }

    /// <summary>
    /// 首选项设置更改总处理函数（合并快捷键与外观更新）
    /// </summary>
    private void OnSettingsChanged(object? sender, (AppSettings settings, SettingsChangeKind changes) e)
    {
        var (settings, changes) = e;

        if (changes.HasFlag(SettingsChangeKind.Shortcuts))
        {
            _shortcutRouter.UpdateSettings(settings.Shortcuts);
            UpdateGroupButtonsShortcutTips();
        }

        if (changes.HasFlag(SettingsChangeKind.Colors))
        {
            CanvasControl.UpdateSettings(settings);
            GroupIndexToBrushConverter.InvalidateCache();
            UpdateGroupButtonColors();
            UpdateLabels();
            if (Navigation.SelectedItem is TranslationTreeItem selectedItem)
                CanvasControl.HighlightLabel(selectedItem.Index);
            RefreshTreeView();
        }
        else if (changes.HasFlag(SettingsChangeKind.LabelSize))
        {
            CanvasControl.UpdateSettings(settings);
            UpdateLabels();
            if (Navigation.SelectedItem is TranslationTreeItem selectedItem)
                CanvasControl.HighlightLabel(selectedItem.Index);
        }

        if (changes.HasFlag(SettingsChangeKind.DligConfig))
        {
            ApplyDligConfig();
        }

        if (changes != SettingsChangeKind.None)
            StatusBar.UpdateStatus("首选项已更新", StatusBarViewModel.StatusType.Info);
    }
    
    /// <summary>
    /// 更新分组切换按钮的快捷键提示
    /// </summary>
    private void UpdateGroupButtonsShortcutTips()
    {
        var shortcuts = _settingsProvider.Current.Shortcuts;
        if (Group0RadioButton != null)
        {
            var shortcutText = ShortcutBindings.KeyGestureToString(shortcuts.ToggleGroup0);
            ToolTip.SetTip(Group0RadioButton, $"切换到框内 ({shortcutText})");
        }
        
        if (Group1RadioButton != null)
        {
            var shortcutText = ShortcutBindings.KeyGestureToString(shortcuts.ToggleGroup1);
            ToolTip.SetTip(Group1RadioButton, $"切换到框外 ({shortcutText})");
        }
    }

    /// <summary>
    /// 应用连字配置：设置编辑器字体、OpenType 特性、快捷输入按钮、树状图译文字体
    /// </summary>
    private void ApplyDligConfig()
    {
        if (_translationTextBox == null)
            return;

        var configName = _settingsProvider.Current.ActiveDligConfig;

        if (string.IsNullOrWhiteSpace(configName))
        {
            _translationTextBox.ClearValue(TextBox.FontFamilyProperty);
            _translationTextBox.ClearValue(TextBox.FontFeaturesProperty);
            Edit.QuickInputSlots.Clear();
            Resources["DligFontFamily"] = null;
            Resources["DligFontFeatures"] = null;
            return;
        }

        var config = DligConfigService.LoadConfig(configName);
        if (config == null)
        {
            _translationTextBox.ClearValue(TextBox.FontFamilyProperty);
            _translationTextBox.ClearValue(TextBox.FontFeaturesProperty);
            Edit.QuickInputSlots.Clear();
            StatusBar.UpdateStatus(
                $"连字配置 '{configName}' 加载失败，已回退到默认",
                StatusBarViewModel.StatusType.Warn);
            Resources["DligFontFamily"] = null;
            Resources["DligFontFeatures"] = null;
            return;
        }

        // 应用快捷输入按钮
        Edit.QuickInputSlots.Clear();
        if (config.QuickInputs != null)
        {
            foreach (var slot in config.QuickInputs)
                Edit.QuickInputSlots.Add(slot);
        }

        // 应用字体和 OpenType 特性
        if (string.IsNullOrWhiteSpace(config.FontFamily))
        {
            Resources["DligFontFamily"] = null;
            Resources["DligFontFeatures"] = null;
            return;
        }

        var fontFamily = new FontFamily(config.FontFamily);
        var typeface = new Typeface(fontFamily);
        var fontInstalled = FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface)
            && string.Equals(glyphTypeface.FamilyName, config.FontFamily, StringComparison.OrdinalIgnoreCase);

        if (!fontInstalled)
        {
            StatusBar.UpdateStatus(
                $"字体 '{config.FontFamily}' 未安装，连字功能不可用",
                StatusBarViewModel.StatusType.Warn);
            Resources["DligFontFamily"] = null;
            Resources["DligFontFeatures"] = null;
            return;
        }

        _translationTextBox.FontFamily = fontFamily;
        QuickInputItemsControl.FontFamily = fontFamily;

        FontFeatureCollection? features = null;
        if (!string.IsNullOrWhiteSpace(config.FontFeatures))
        {
            features = new FontFeatureCollection();
            foreach (var part in config.FontFeatures.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                features.Add(FontFeature.Parse(part));

            _translationTextBox.FontFeatures = features;
            QuickInputItemsControl.FontFeatures = features;
        }

        Resources["DligFontFamily"] = fontFamily;
        Resources["DligFontFeatures"] = features;
    }

    /// <summary>
    /// 获取当前图片的标签列表（常用守卫+查找模式的封装）
    /// </summary>
    /// <returns>如果当前有文档且图片有效且存在标签，返回标签列表；否则返回 null</returns>
    private List<LabelItem>? TryGetCurrentLabels()
    {
        if (Document.TranslationData == null
            || string.IsNullOrEmpty(CanvasControl.CurrentImagePath))
            return null;

        string imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            return null;

        return labels;
    }
    
    /// <summary>
    /// 保存当前窗口尺寸/位置/最大化状态到设置文件
    /// </summary>
    private void SaveWindowBounds()
    {
        try
        {
            var s = _settingsProvider.Current;
            if (WindowState == WindowState.Maximized)
            {
                System.Diagnostics.Debug.WriteLine($"[Save] Maximized, saving _normal*: {_normalWidth}x{_normalHeight} @ ({_normalPosition.X},{_normalPosition.Y})");
                s.WindowMaximized = true;
                s.WindowWidth = _normalWidth;
                s.WindowHeight = _normalHeight;
                s.WindowX = _normalPosition.X;
                s.WindowY = _normalPosition.Y;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Save] Normal, saving current: {Width}x{Height} @ {Position}");
                s.WindowMaximized = false;
                s.WindowWidth = Width;
                s.WindowHeight = Height;
                s.WindowX = Position.X;
                s.WindowY = Position.Y;
            }
            _settingsProvider.Save();
        }
        catch
        {
            // 窗口尺寸保存失败不影响关闭
        }
    }
    
    /// <summary>
    /// 窗口关闭时清理所有资源
    /// </summary>
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // 检查是否有未保存的更改
        if (Document.HasDocument && Document.IsDirty)
        {
            e.Cancel = true; // 阻止立即关闭
            var canClose = await Document.ConfirmAndSaveAsync();
            if (canClose)
            {
                // 确认后强制关闭
                Document.ForceCloseDocument();
                Close();
            }
            return;
        }
        
        // 取消订阅事件
        // ImageContainer 已迁入 AnnotationCanvas，无需在此处理
        this.Closing -= OnWindowClosing;

        SaveWindowBounds();
        _sizeDebounce?.Stop();
        _sizeDebounce = null;
        
        // 清空历史记录
        ViewModel.History.Clear();
        
        // 清除标注控件（ClearCanvas 内部会 Dispose 图片并重置 _isFirstImageLoaded）
        CanvasControl.ClearCanvas();
        
        // 清空树视图数据
        Navigation.TreeItems.Clear();
        
        // 清空其他数据（TranslationData 由 DocumentViewModel 管理，无需手动清理）
        Navigation.ImageFolderPath = null;
        Navigation.ImageNames.Clear();
        
        // 强制退出整个进程
        Environment.Exit(0);
    }
    
    /// <summary>
    /// 未保存更改确认对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<UnsavedChangesResult> ShowUnsavedChangesDialogAsync(string message)
    {
        var result = UnsavedChangesResult.Cancel;

        var dialog = new Window
        {
            Title = "保存",
            Width = 420,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None },
            Background = Brushes.White
        };

        // 根布局：上方内容区（*）+ 下方按钮栏（Auto）
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // 内容区：DockPanel 实现左图标 + 右文本并排布局
        var contentPanel = new DockPanel { Margin = new Thickness(24, 20, 24, 12) };

        // 叹号图标（FluentIcon 基于字体渲染，用 FontSize 控制大小）
        var warningIcon = new FluentIcons.Avalonia.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Warning,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = 48,
            // Width = 36,
            // Height = 36,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        DockPanel.SetDock(warningIcon, Dock.Left);
        contentPanel.Children.Add(warningIcon);

        // 提示文本（顶部留出偏移以与图标视觉中心对齐）
        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        };
        contentPanel.Children.Add(textBlock);

        Grid.SetRow(contentPanel, 0);
        rootGrid.Children.Add(contentPanel);

        // 底部按钮区域（含顶部分隔线）
        var buttonArea = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 6,
            Margin = new Thickness(0, 12, 16, 16),
        };

        var saveButton = new Button { Content = "保存", Width = 80 };
        var discardButton = new Button { Content = "不保存", Width = 80 };
        var cancelButton = new Button { Content = "取消", Width = 80 };

        saveButton.Click += (s, e) => { result = UnsavedChangesResult.Save; dialog.Close(); };
        discardButton.Click += (s, e) => { result = UnsavedChangesResult.Discard; dialog.Close(); };
        cancelButton.Click += (s, e) => { result = UnsavedChangesResult.Cancel; dialog.Close(); };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(discardButton);
        buttonPanel.Children.Add(cancelButton);
        buttonArea.Children.Add(buttonPanel);

        Grid.SetRow(buttonArea, 1);
        rootGrid.Children.Add(buttonArea);

        dialog.Content = rootGrid;
        dialog.Measure(new Size(420, 140));
        dialog.Arrange(new Rect(0, 0, 420, 140));

        await dialog.ShowDialog(this);

        return result;
    }

    /// <summary>
    /// 图片选择对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<ImageSelectionResult?> ShowImageSelectionDialogAsync(
        List<string> imageFiles, string defaultFileName)
    {
        var selectionWindow = new Views.ImageSelectionWindow(imageFiles, defaultFileName);
        selectionWindow.Owner = this;

        var dialogResult = await selectionWindow.ShowDialog<bool>(this);

        if (!dialogResult || selectionWindow.SelectedImagePaths.Count == 0)
            return null;

        return new ImageSelectionResult
        {
            SelectedImagePaths = selectionWindow.SelectedImagePaths,
            FileName = selectionWindow.FileName
        };
    }

    /// <summary>
    /// 文件关联管理器对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<ImageAssociationResult?> ShowImageAssociationDialogAsync(
        List<ImageAssociationItem> items, string imageFolderPath)
    {
        var associationWindow = new ImageAssociationWindow(items, imageFolderPath);
        var dialogResult = await associationWindow.ShowDialog<bool>(this);
        return dialogResult ? associationWindow.Result : null;
    }

    /// <summary>
    /// DocumentViewModel.DocumentOpened 事件处理
    /// </summary>
    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        Navigation.InitializeNavigation(e.ImageFolderPath, e.ImageNames, e.ImagePathMapping);

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
    /// 重建当前视图以同步 UI（用于 Undo/Redo 后刷新界面）
    /// </summary>
    private void RebuildCurrentView()
    {
        if (Document.TranslationData == null)
            return;
        
        // 在重建前记住当前选中的标签索引
        int? previouslySelectedLabelIndex = null;
        if (Navigation.SelectedItem is TranslationTreeItem currentItem)
        {
            previouslySelectedLabelIndex = currentItem.Index;
        }
        
        // 【新增】如果有待选中的新标签（添加标签操作），优先使用它
        if (CanvasWorkspace.PendingNewLabelIndex.HasValue)
        {
            previouslySelectedLabelIndex = CanvasWorkspace.PendingNewLabelIndex;
        }
        
        // 重新构建树视图
        Navigation.BuildTreeView(Document.TranslationData);

        // 不清空 TextBox，保留用户正在编辑的内容
        // TextBox 的值会在后续的 SelectionChanged 中被正确设置

        // ======================= FIX START =======================
        // 更新画布标注
        // 【修复核心】：如果当前正在按下鼠标拖拽某个标签，则跳过全量图形销毁与重建，
        // 保护当前正在捕获鼠标事件的原生控件不被销毁，从而保持拖拽的连续性。
        if (!CanvasControl.IsDraggingLabel)
        {
            UpdateLabels();
        }
        else
        {
            // 如果跳过重建，也要确保同步其可能因选中项变化带来的高亮状态
            if (previouslySelectedLabelIndex.HasValue)
            {
                CanvasControl.HighlightLabel(previouslySelectedLabelIndex.Value);
            }
        }
        // ======================= FIX END =======================
        
        // 尝试恢复当前选中的图片
        if (!string.IsNullOrEmpty(CanvasControl.CurrentImagePath))
        {
            var imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
            var treeItem = Navigation.FindTreeItemByImageName(imageName);
            if (treeItem != null)
            {
                Navigation.CurrentTreeItem = treeItem;
                treeItem.IsExpanded = true;

                if (previouslySelectedLabelIndex.HasValue)
                {
                    // 恢复焦点到特定的标签项
                    var labelItem = treeItem.Translations.FirstOrDefault(t => t.Index == previouslySelectedLabelIndex.Value);
                    if (labelItem != null)
                    {
                        Navigation.SelectedItem = labelItem;
                    }
                    else
                    {
                        Navigation.SelectedItem = treeItem;
                    }
                }
                else
                {
                    Navigation.SelectedItem = treeItem;
                }
            }
        }
        
        // 【新增】如果有待选中的新标签已被选中，聚焦到文本框
        // 根据设置决定是否自动聚焦
        if (CanvasWorkspace.PendingNewLabelIndex.HasValue && _settingsProvider.Current.AutoFocusTextBox)
        {
            // 清除待选中状态后，聚焦到文本框
            CanvasWorkspace.ClearPendingNewLabelIndex();

            // 延迟聚焦到文本框，确保 UI 已完成重建
            Dispatcher.UIThread.Post(() =>
            {
                _translationTextBox?.Focus();
            }, DispatcherPriority.Loaded);
        }
        else if (CanvasWorkspace.PendingNewLabelIndex.HasValue)
        {
            // 如果不需要自动聚焦，仅清除待选中状态
            CanvasWorkspace.ClearPendingNewLabelIndex();
        }
        
    }
    
    /// <summary>
    /// 编辑模式变更事件处理（由 EditViewModel.EditModeChanged 触发）
    /// </summary>
    private void OnEditModeChanged(object? sender, EventArgs e)
    {
        CanvasControl.IsEditMode = Edit.IsEditMode;
        if (Edit.IsEditMode)
        {
            UpdateGroupButtonColors();
            // ApplyDligConfig 已移入选中标记时的光标链（Loaded→Render），避免此项与光标竞态
        }
    }

    /// <summary>
    /// 分组变更事件处理（由 EditViewModel.GroupChanged 触发）
    /// </summary>
    private void OnGroupChanged(object? sender, EventArgs e)
    {
        // 同步 RadioButton 选中状态
        var groupIndex = Edit.CurrentGroupIndex;
        if (groupIndex == 0 && Group0RadioButton != null)
            Group0RadioButton.IsChecked = true;
        else if (groupIndex == 1 && Group1RadioButton != null)
            Group1RadioButton.IsChecked = true;
        
        // 统一更新分组按钮颜色
        UpdateGroupButtonColors();
    }

    /// <summary>
    /// 快捷输入按钮点击事件（占位处理函数）
    /// 后续可在此实现插入预设文本或特殊符号的功能
    /// </summary>
    private void OnToolbarScrollWheel(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.Extent.Width > sv.Viewport.Width)
        {
            var offset = sv.Offset;
            var newX = offset.X - e.Delta.Y * 20;
            newX = Math.Max(0, Math.Min(newX, sv.Extent.Width - sv.Viewport.Width));
            sv.Offset = new Vector(newX, offset.Y);
            e.Handled = true;
        }
    }

    private void OnQuickInputButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not QuickInputSlot slot) return;
        if (string.IsNullOrEmpty(slot.Character)) return;
        if (Navigation.SelectedTranslationItem is not { } item) return;
        if (_translationTextBox is not { IsEnabled: true }) return;

        var caretIndex = _translationTextBox.CaretIndex;
        var current = item.Text ?? "";
        item.Text = current.Insert(caretIndex, slot.Character);

        _translationTextBox.CaretIndex = caretIndex + slot.Character.Length;
        _translationTextBox.Focus();
        CommitCurrentEdit();
    }

    /// <summary>
    /// 分组选择改变
    /// </summary>
    private void OnGroupSelectionChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton)
        {
            if (radioButton == Group0RadioButton)
            {
                Edit.SwitchGroupCommand.Execute(0);
            }
            else if (radioButton == Group1RadioButton)
            {
                Edit.SwitchGroupCommand.Execute(1);
            }
            // RadioButton 同步和颜色更新由 OnGroupChanged 统一处理
        }
    }

    /// <summary>
    /// 更新分组按钮的颜色样式
    /// </summary>
    private void UpdateGroupButtonColors()
    {
        if (Group0RadioButton == null || Group1RadioButton == null)
            return;

        try
        {
            var settings = _settingsProvider.Current;

            // Group0 (框内) - 分组索引为1
            var group0ColorHex = settings.Colors.GroupColors.GetValueOrDefault(1, "#E74856");
            var group0Color = Avalonia.Media.Color.Parse(group0ColorHex);
            var group0HoverColor = AdjustBrightness(group0Color, 1.2); // 增加亮度
            var group0PressedColor = AdjustBrightness(group0Color, 0.7); // 减少亮度

            // Group1 (框外) - 分组索引为2
            var group1ColorHex = settings.Colors.GroupColors.GetValueOrDefault(2, "#1E90FF");
            var group1Color = Avalonia.Media.Color.Parse(group1ColorHex);
            var group1HoverColor = AdjustBrightness(group1Color, 1.2);
            var group1PressedColor = AdjustBrightness(group1Color, 0.7);

            // 更新Window Resources中的颜色
            UpdateColorResource("Group0ColorBrush", group0Color);
            UpdateColorResource("Group1ColorBrush", group1Color);
            UpdateColorResource("Group0ColorHoverBrush", group0HoverColor);
            UpdateColorResource("Group1ColorHoverBrush", group1HoverColor);
            UpdateColorResource("Group0ColorPressedBrush", group0PressedColor);
            UpdateColorResource("Group1ColorPressedBrush", group1PressedColor);
        }
        catch
        {
            // 如果出错，使用默认颜色
        }
    }

    /// <summary>
    /// 更新Window资源中的颜色
    /// </summary>
    private void UpdateColorResource(string key, Avalonia.Media.Color color)
    {
        if (Resources.TryGetValue(key, out var existingBrush) && existingBrush is Avalonia.Media.SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    /// <summary>
    /// 调整颜色亮度
    /// </summary>
    private static Avalonia.Media.Color AdjustBrightness(Avalonia.Media.Color color, double factor)
    {
        var r = (byte)Math.Min(255, (int)(color.R * factor));
        var g = (byte)Math.Min(255, (int)(color.G * factor));
        var b = (byte)Math.Min(255, (int)(color.B * factor));
        return Avalonia.Media.Color.FromArgb(color.A, r, g, b);
    }


    /// <summary>
    /// Text 变更时主动重置 CaretIndex 到末尾
    /// </summary>
    private void OnTranslationTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        var tb = _translationTextBox;
        if (tb == null) return;

        var len = tb.Text?.Length ?? 0;
        tb.CaretIndex = 0; // Avalonia 对 CaretIndex 有短路优化，先重置到开头触发 invalidation
        tb.CaretIndex = len;
        tb.SelectionStart = len;
        tb.SelectionEnd = len;
    }

    /// <summary>
    /// 文本框失去焦点时，直接结算当前文本
    /// </summary>
    private void OnTranslationTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUI) return;

        // 失去焦点时，直接结算当前文本
        CommitCurrentEdit();
    }

    /// <summary>
    /// 提交当前编辑的文本到撤销/重做历史栈中
    /// 核心理念：直接对比 UI 最新文本与底层模型数据的差异，彻底抛弃状态变量缓存
    /// </summary>
    private void CommitCurrentEdit()
    {
        if (!_isInitialized || !Edit.IsEditMode) return;
        if (TryGetCurrentLabels() is not { } labels) return;
        if (Navigation.SelectedTranslationItem is not { } currentTreeItem) return;

        var labelItem = labels.FirstOrDefault(l => l.TextIndex == currentTreeItem.Index);
        if (labelItem == null) return;

        string newText = currentTreeItem.Text ?? string.Empty;
        string oldText = labelItem.Text ?? string.Empty;

        if (oldText != newText)
        {
            var command = new ChangeTextCommand(labelItem, oldText, newText);
            ViewModel.History.ExecuteCommand(command);
        }
    }


    /// <summary>
    /// 全局快捷键拦截（隧道路由，在子控件处理前触发）
    /// 用于接管并统一处理 TextBox 等子控件的撤销/重做快捷键冲突
    /// </summary>
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // 【防卫】初始化未完成前只处理 Ctrl+Enter（不依赖任何 VM）
        if (!_isInitialized)
        {
            var modifiersEarly = e.KeyModifiers;
            bool isCtrlPressedEarly = (modifiersEarly & KeyModifiers.Control) != 0 || (modifiersEarly & KeyModifiers.Meta) != 0;
            if (isCtrlPressedEarly && (e.Key == Key.Return || e.Key == Key.Enter))
            {
                if (_translationTextBox != null && _translationTextBox.IsFocused)
                {
                    CommitCurrentEdit();
                    e.Handled = true;
                }
            }
            return;
        }

        var modifiers = e.KeyModifiers;
        // 兼容 Windows (Control) 和 Mac (Meta)
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;
        bool isShiftPressed = (modifiers & KeyModifiers.Shift) != 0;

        // ========== 撤销/重做：通过隧道拦截，手动路由到 HistoryViewModel Command ==========
        // 必须在隧道阶段拦截，防止 TextBox 等原生控件触发内置撤销逻辑
        if (isCtrlPressed && e.Key == Key.Z)
        {
            if (isShiftPressed)
            {
                ViewModel.History.RedoCommand.Execute(null);
            }
            else
            {
                ViewModel.History.UndoCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (isCtrlPressed && e.Key == Key.Y)
        {
            ViewModel.History.RedoCommand.Execute(null);
            e.Handled = true;
        }
        
        // 通过 ShortcutRouter 匹配可配置快捷键
        var currentGesture = new KeyGesture(e.Key, e.KeyModifiers);
        bool isTextBoxFocused = _translationTextBox != null && _translationTextBox.IsFocused;
        var action = _shortcutRouter.MatchKeyGesture(currentGesture, isTextBoxFocused);
        if (action.HasValue)
        {
            ExecuteShortcutAction(action.Value);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 执行快捷键动作（由 ShortcutRouter 匹配后统一调用）
    /// </summary>
    private void ExecuteShortcutAction(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.NavigateUp:
                Navigation.NavigateUpCommand.Execute(null);
                break;
            case ShortcutAction.NavigateDown:
                Navigation.NavigateDownCommand.Execute(null);
                break;
            case ShortcutAction.CopyText:
                if (Navigation.SelectedItem is TranslationTreeItem item)
                {
                    CopyToClipboard(item.Text);
                    StatusBar.UpdateStatus($"已复制: {item.Text}", StatusBarViewModel.StatusType.Info);
                }
                break;
            case ShortcutAction.DeleteLabel:
                DeleteSelectedLabel();
                break;
            case ShortcutAction.OpenFile:
                ViewModel.Document.OpenCommand.Execute(null);
                break;
            case ShortcutAction.SaveFile:
                ViewModel.Document.SaveCommand.Execute(null);
                break;
            case ShortcutAction.SwitchToGroup0:
                Edit.SwitchGroupCommand.Execute(0);
                break;
            case ShortcutAction.SwitchToGroup1:
                Edit.SwitchGroupCommand.Execute(1);
                break;
        }
    }

    /// <summary>
    /// 根据索引选中文本节点并在树视图中聚焦
    /// </summary>
    private void SelectLabelByIndex(int labelIndex)
    {
        // 委托到 NavigationViewModel 执行核心选中逻辑
        Navigation.SelectLabelByIndex(labelIndex);
        
        // UI 层补充操作
        if (Navigation.SelectedItem != null)
        {
            ImageTreeView.Focus();
            StatusBar.UpdateStatus($"已选中标注 #{labelIndex}", StatusBarViewModel.StatusType.Info);
        }
    }
    
    /// <summary>
    /// 在指定坐标新建标签（如果该位置没有现有标签）
    /// </summary>
    private void AddNewLabel(double imageX, double imageY)
    {
        if (CanvasControl.CurrentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(CanvasControl.CurrentImagePath) || Navigation.CurrentTreeItem == null)
            return;

        string imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
        
        // 确保字典中有该图片的数据列表（TryGetCurrentLabels 不适用，因为此处需要创建新列表）
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
        {
            labels = new List<LabelItem>();
            Document.TranslationData.ImageLabels[imageName] = labels;
        }

        // 计算新的编号 (TextIndex)，取当前最大值 + 1
        int nextIndex = labels.Any() ? labels.Max(l => l.TextIndex) + 1 : 1;

        // 计算归一化坐标 (0.0 ~ 1.0)
        double normX = imageX / CanvasControl.CurrentImage!.Size.Width;
        double normY = imageY / CanvasControl.CurrentImage!.Size.Height;

        // 创建底层数据
        var newLabel = new LabelItem
        {
            ImageName = imageName,
            TextIndex = nextIndex,
            X = normX,
            Y = normY,
            GroupIndex = Edit.CurrentGroupIndex + 1, // 从1开始：0→1, 1→2
            Text = ""
        };
        
        // 创建并执行 AddLabelCommand（命令会自动刷新 UI）
        CanvasWorkspace.AddLabel(labels, newLabel, nextIndex);

        // 注意：UI 刷新由 HistoryManager.HistoryChanged 事件处理
    }
    
    
    /// <summary>
    /// 显示主界面，隐藏欢迎屏幕
    /// </summary>
    private void ShowMainContent()
    {
        WelcomeViewControl.IsVisible = false;
        MainContentPanel.IsVisible = true;
        
        Edit.CanToggleEditMode = true;
        Edit.IsEditMode = false;
    }
    
    /// <summary>
    /// 显示欢迎屏幕，隐藏主界面
    /// </summary>
    private void ShowWelcomeScreen()
    {
        WelcomeViewControl.IsVisible = true;
        MainContentPanel.IsVisible = false;
        
        Edit.CanToggleEditMode = false;
        Edit.IsEditMode = false;
    }
    
    private async void OnAbout(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new Views.AboutWindow();
        await aboutWindow.ShowDialog(this);
    }
    
    // ==================== ImageContainer 事件处理 ====================
    
    private void OnCanvasAddLabelRequested(object? sender, (double pixelX, double pixelY) coords)
    {
        // 检查是否在编辑模式
        if (!Edit.IsEditMode)
            return;
        
        // 检查点击位置是否已有现有标签
        if (Document.TranslationData == null || string.IsNullOrEmpty(CanvasControl.CurrentImagePath))
            return;
        
        var labels = TryGetCurrentLabels();
        if (labels == null)
        {
            // 当前图片尚无标签，直接添加
            AddNewLabel(coords.pixelX, coords.pixelY);
            return;
        }
        
        // 检查是否命中现有标签
        int? hitIndex = CanvasWorkspace.FindLabelAtPosition(coords.pixelX, coords.pixelY, 
            CanvasControl.CurrentImage?.Size.Width ?? 0, 
            CanvasControl.CurrentImage?.Size.Height ?? 0, 
            labels);
        
        if (hitIndex.HasValue)
        {
            _isSelectionFromCanvas = true;
            SelectLabelByIndex(hitIndex.Value);
        }
        else
        {
            _isSelectionFromCanvas = true;
            AddNewLabel(coords.pixelX, coords.pixelY);
        }
    }
    
    /// <summary>
    /// 画布标签拖拽结束后的位置变更处理
    /// </summary>
    private void OnCanvasLabelMoved(object? sender, (int textIndex, double oldNormX, double oldNormY, double newNormX, double newNormY) args)
    {
        if (TryGetCurrentLabels() is not { } labels)
            return;
        
        var label = labels.FirstOrDefault(l => l.TextIndex == args.textIndex);
        if (label != null)
        {
            _isSelectionFromCanvas = true;
            CanvasWorkspace.MoveLabel(label, args.oldNormX, args.oldNormY, args.newNormX, args.newNormY);
        }
    }
    
    /// <summary>
    /// 计算适应容器的初始变换（Fit模式）—— 委托给 CanvasWorkspaceViewModel
    /// </summary>
    private void CalculateFitTransform()
    {
        CanvasControl.CalculateFitTransform();
    }
    
    /// <summary>
    /// 保存当前图片的 fit 缩放比例到对应的树视图项
    /// </summary>
    private void SaveCurrentFitScale(double fitScale)
    {
        if (CanvasControl.CurrentImagePath == null || Navigation.TreeItems.Count == 0) return;
        
        string imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
        var item = Navigation.FindTreeItemByImageName(imageName);
        if (item != null)
        {
            item.FitScale = fitScale;
            Navigation.CurrentTreeItem = item;
        }
    }
    
    // ==================== 图片加载 ====================

    /// <summary>
    /// 更新标注：根据当前图片的标注数据，在画布上显示编号
    /// </summary>
    private void UpdateLabels()
    {
        // 没有图片或翻译数据时返回
        if (CanvasControl.CurrentImage == null || TryGetCurrentLabels() is not { } labels)
            return;

        // 通过 CanvasControl 更新标注显示，并高亮当前选中项
        int? highlightIndex = (Navigation.SelectedItem is TranslationTreeItem selectedTranslation) 
            ? selectedTranslation.Index 
            : null;
        
        CanvasControl.UpdateLabels(labels, CanvasControl.CurrentImage.Size.Width, CanvasControl.CurrentImage.Size.Height, highlightIndex);
    }
    
    /// <summary>
    /// 加载当前图片
    /// </summary>
    private void LoadCurrentImage()
    {
        if (Navigation.ImageNames.Count == 0 || string.IsNullOrEmpty(Navigation.ImageFolderPath))
            return;
        
        if (Navigation.CurrentImageIndex < 0 || Navigation.CurrentImageIndex >= Navigation.ImageNames.Count)
            return;
        
        var imageName = Navigation.ImageNames[Navigation.CurrentImageIndex];
        var imagePath = ResolveImagePath(imageName);
        
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            LoadImage(imagePath);
        }
        else
        {
            CanvasControl.ShowErrorPlaceholder(imageName);
            StatusBar.UpdateStatus($"找不到指定的文件: {imageName}", StatusBarViewModel.StatusType.Warn);
        }
    }
    
    private string ResolveImagePath(string imageName)
    {
        if (Navigation.ImagePathMapping.TryGetValue(imageName, out var mappedPath))
            return mappedPath;

        return Path.Combine(Navigation.ImageFolderPath!, imageName);
    }
    
    private void LoadImage(string imagePath)
    {
        try
        {
            // 通过 CanvasControl 加载图片
            CanvasControl.LoadImage(imagePath);
            
            // 找到当前图片对应的树视图项
            string imageName = Path.GetFileName(imagePath);
            var treeItem = Navigation.FindTreeItemByImageName(imageName);
            if (treeItem != null)
            {
                Navigation.CurrentTreeItem = treeItem;
            }
            
            // 通知 CanvasWorkspace 图片尺寸
            CanvasWorkspace.UpdateImageSize(new Size(CanvasControl.CurrentImage!.Size.Width, CanvasControl.CurrentImage!.Size.Height));
            
            // 首次加载时延迟计算适应容器的初始变换，等待布局完成
            if (!CanvasControl.IsFirstImageLoaded)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // 等待布局完成
                    
                    // 在 UI 线程上执行
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        CalculateFitTransform();
                        CanvasControl.MarkFirstImageLoaded();
                        
                        // 更新标注显示
                        UpdateLabels();
                    });
                });
            }
            else
            {
                // 非首次加载直接应用已有的变换
                CanvasControl.ApplyTransform();
                StatusBar.UpdateZoom(CanvasWorkspace.ZoomPercent);
                // 更新标注显示
                UpdateLabels();
            }
            
            // 更新状态栏显示当前图片信息
            if (Navigation.ImageNames.Count > 0)
            {
                StatusBar.UpdateStatus($"[{Navigation.CurrentImageIndex + 1}/{Navigation.ImageNames.Count}] {Path.GetFileName(imagePath)}");
            }
            else
            {
                StatusBar.UpdateStatus($"已加载图片: {Path.GetFileName(imagePath)}", StatusBarViewModel.StatusType.Info);
            }
        }
        catch (Exception ex)
        {
            StatusBar.UpdateStatus($"加载图片失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
        }
    }
    
    // ==================== 辅助方法 ====================
    
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
    /// 复制选中项的文本（右键菜单调用）
    /// </summary>
    private void OnCopySelectedText(object? sender, RoutedEventArgs e)
    {
        var selectedItem = Navigation.SelectedItem;
        if (selectedItem is TranslationTreeItem translationItem)
        {
            CopyToClipboard(translationItem.Text);
            StatusBar.UpdateStatus($"已复制: {translationItem.Text}", StatusBarViewModel.StatusType.Info);
        }
    }
    
    /// <summary>
    /// 删除选中的标记（右键菜单调用）
    /// </summary>
    private void OnDeleteSelectedLabel(object? sender, RoutedEventArgs e)
    {
        DeleteSelectedLabel();
    }

    /// <summary>
    /// 切换选中标记的分组（右键菜单调用）
    /// </summary>
    private void OnToggleGroup(object? sender, RoutedEventArgs e)
    {
        // 切换分组前先提交当前正在编辑的文本
        CommitCurrentEdit();
        
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
        // 删除前先提交当前正在编辑的文本
        CommitCurrentEdit();
        
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
    
    /// <summary>
    /// 树视图选择变更事件（处理图片切换 & 自动折叠展开）
    /// </summary>
    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
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
            CanvasControl.HighlightLabel(targetChildItem.Index);

            double currentScale = CanvasWorkspace.ZoomPercent / 100;
            double fitScale = targetRootItem?.FitScale ?? 1.0;
            if (currentScale > fitScale && !_isSelectionFromCanvas)
                CenterOnLabel(targetChildItem.Index);
            _isSelectionFromCanvas = false;

            if (Edit.IsEditMode && _settingsProvider.Current.AutoFocusTextBox)
            {
                Dispatcher.UIThread.Post(
                    () => _translationTextBox?.Focus(),
                    DispatcherPriority.Loaded);
            }
        }
        else if (selectedItem is ImageTreeItem)
        {
            CanvasControl.HighlightLabel(-1);
        }
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
    // 状态机：IDLE → PRESSED → DRAGGING → IDLE
    //   PRESSED: PointerPressed 后，等待超过防抖阈值
    //   DRAGGING: 超过阈值后捕获指针，通过 hit-testing 查找放置目标

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

            if (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)
            {
                // 进入 DRAGGING 状态
                _isTreeItemDragging = false;
                _isDragActive = true;
                CommitCurrentEdit();

                // 捕获指针 — 确保后续所有 Pointer 事件都发送到此控件
                e.Pointer.Capture((Control)sender!);

                // 设置拖拽光标
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
            return;
        }

        // 阶段2：DRAGGING — 通过 hit-testing 查找放置目标
        if (_isDragActive && _draggedTreeItem != null && _imageTreeView != null)
        {
            var treeViewPos = e.GetPosition(_imageTreeView);
            var newTarget = FindDropTarget(treeViewPos);

            if (newTarget != _currentDropTarget)
            {
                _currentDropTarget = newTarget;
                // 更新光标：有效目标 → Move，无效 → No
                Cursor = (newTarget != null)
                    ? new Cursor(StandardCursorType.SizeAll)
                    : new Cursor(StandardCursorType.No);
            }
        }
    }

    private void OnTreeViewItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragActive && _draggedTreeItem != null && _currentDropTarget != null)
        {
            // 执行重排
            PerformReorder(_draggedTreeItem, _currentDropTarget);
        }

        // 清理所有拖拽状态
        if (_isDragActive)
        {
            e.Pointer.Capture(null!); // 释放指针捕获
        }
        _isDragActive = false;
        _isTreeItemDragging = false;
        _draggedTreeItem = null;
        _currentDropTarget = null;
        Cursor = null; // 恢复默认光标
    }

    /// <summary>
    /// 在 TreeView 中通过坐标 hit-testing 查找有效的放置目标。
    /// 仅允许同图片下的 TranslationTreeItem 作为目标。
    /// </summary>
    private TranslationTreeItem? FindDropTarget(Point positionInTreeView)
    {
        if (_imageTreeView == null || _draggedTreeItem == null) return null;

        foreach (var item in _imageTreeView.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (item.DataContext is TranslationTreeItem treeItem)
            {
                var itemPos = item.TranslatePoint(new Point(0, 0), _imageTreeView);
                if (itemPos.HasValue)
                {
                    var bounds = new Rect(itemPos.Value, item.Bounds.Size);
                    if (bounds.Contains(positionInTreeView))
                    {
                        // 不允许放在自身上
                        if (treeItem == _draggedTreeItem) return null;

                        // 仅允许同图片下的项
                        var sourceParent = Navigation.GetParentImageItem(_draggedTreeItem);
                        var targetParent = Navigation.GetParentImageItem(treeItem);
                        if (sourceParent != null && sourceParent == targetParent)
                            return treeItem;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 执行拖拽重排：将 sourceItem 移动到 targetItem 的位置。
    /// </summary>
    private void PerformReorder(TranslationTreeItem sourceItem, TranslationTreeItem targetItem)
    {
        var parentImageItem = Navigation.GetParentImageItem(sourceItem);
        if (parentImageItem == null || parentImageItem != Navigation.GetParentImageItem(targetItem)) return;

        if (Document.TranslationData != null)
        {
            string imageName = parentImageItem.ImageName;
            if (Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            {
                var sourceModel = labels.FirstOrDefault(l => l.TextIndex == sourceItem.Index);
                var targetModel = labels.FirstOrDefault(l => l.TextIndex == targetItem.Index);

                if (sourceModel != null && targetModel != null)
                {
                    int targetIndex = labels.IndexOf(targetModel);
                    CanvasWorkspace.ReorderLabels(labels, sourceModel, targetIndex, targetIndex + 1);
                }
            }
        }
    }
    
    // ==================== 文件拖放打开 ====================

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null || files.Length == 0)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var hasTxtFile = files.Any(f =>
            f.Path.IsFile &&
            f.Path.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        e.DragEffects = hasTxtFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        var txtFile = files?.FirstOrDefault(f =>
            f.Path.IsFile &&
            f.Path.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        if (txtFile == null)
            return;

        var filePath = txtFile.Path.LocalPath;
        await Document.OpenTranslationFileAsync(filePath);
    }
}