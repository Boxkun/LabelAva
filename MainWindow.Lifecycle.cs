using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LabelAva.Services;
using LabelAva.Views;
using LabelAva.ViewModels;


namespace LabelAva;

/// <summary>
/// MainWindow 的「Lifecycle」部分。
/// 窗口生命周期：构造后的异步初始化、首帧时机、窗口尺寸/位置持久化、关闭流程与视图状态切换。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 首次打开窗口时的处理：等待首帧渲染完成后显示窗口
    /// </summary>
    private async void OnWindowFirstOpened(object? sender, EventArgs e)
    {
        this.Opened -= OnWindowFirstOpened;

        // 等待首帧渲染完成（Render 优先级确保布局+渲染 pass 已执行）
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        // 延迟最大化：先以 Normal 尺寸呈现，让 OS 记录 Normal 尺寸后最大化
        if (_windowState.RestoreMaximized)
            WindowState = WindowState.Maximized;

        // 首帧已上屏，安全地显示窗口
        this.Opacity = 1;

        // 异步执行重工作
        await InitializeAsync();

        // Normal 尺寸的初值由 WindowStateTracker 在构造时从 settings 读入，
        // 避免最大化状态下把膨胀后的尺寸当成 Normal 尺寸记下来。
        _sizeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _sizeDebounce.Tick += (_, _) =>
        {
            _sizeDebounce.Stop();
            if (WindowState != WindowState.Normal)
            {
                System.Diagnostics.Debug.WriteLine($"[Debounce] SKIPPED, state={WindowState}");
                return;
            }
            _windowState.RememberNormalBounds(new WindowBounds(Width, Height, Position));
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
            {
                _translationTextBox.GotFocus += OnTranslationTextBoxGotFocus;
                // Ctrl+Enter 提交：气泡阶段处理（handledEventsToo:true 确保即使事件已处理也触发）
                _translationTextBox.AddHandler(InputElement.KeyDownEvent, OnTranslationTextBoxKeyDown,
                    handledEventsToo: true);
                // 崩溃恢复：逐字写入恢复文件（200ms 防抖）
                _translationTextBox.TextChanged += OnTranslationTextBoxTextChanged;
            }

            // 崩溃恢复防抖定时器
            var debounceMs = Math.Clamp(_settingsProvider.Current.RecoveryDebounceMs, 100, 2000);
            _recoveryDebounce = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(debounceMs)
            };
            _recoveryDebounce.Tick += (_, _) =>
            {
                _recoveryDebounce.Stop();
                Document.WriteImmediateRecovery();
            };

            // ---- Phase 3: VM 创建（有依赖顺序） ----
            var historyManager = new HistoryManager();
            ViewModel.History = new HistoryViewModel(historyManager, StatusBar);
            ViewModel.History.HistoryStateChanged += OnHistoryStateChanged;

            ViewModel.CanvasWorkspace = new CanvasWorkspaceViewModel(ViewModel.History, StatusBar);
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
                new DialogService(this),
                _settingsProvider
            );
            ViewModel.Document.BeforeSave = null;
            ViewModel.Document.DocumentOpened += OnDocumentOpened;
            ViewModel.Document.DocumentClosed += OnDocumentClosed;

            // 拖拽控制器依赖 Navigation / Document / CanvasWorkspace，必须在 Phase 3 之后创建
            InitializeTreeDragDrop();

            // ---- Phase 4: Canvas 初始化 ----
            CanvasControl.SettingsProvider = _settingsProvider;
            CanvasControl.UpdateSettings(_settingsProvider.Current); // 初始化鼠标配置
            GroupIndexToBrushConverter.Initialize(_settingsProvider);
            CanvasControl.SelectLabelByIndex = SelectLabelByIndex;
            CanvasControl.IsEditMode = Edit.IsEditMode;
            CanvasControl.LabelClicked += (_, labelIndex) =>
            {
                _isSelectionFromCanvas = true;
                SelectLabelByIndex(labelIndex);
            };
            CanvasControl.AddLabelRequested += OnCanvasAddLabelRequested;
            CanvasControl.LabelMoved += OnCanvasLabelMoved;
            CanvasControl.LabelDeleteRequested += OnCanvasLabelDeleteRequested;
            CanvasControl.LabelContextMenuRequested += OnCanvasLabelContextMenuRequested;

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
    /// 保存当前窗口尺寸/位置/最大化状态到设置文件
    /// </summary>
    private void SaveWindowBounds()
    {
        try
        {
            var current = new WindowBounds(Width, Height, Position);
            var isMaximized = WindowState == WindowState.Maximized;
            var toPersist = _windowState.ResolveBoundsToPersist(current, isMaximized);
            System.Diagnostics.Debug.WriteLine(
                $"[Save] {(isMaximized ? "Maximized, saving normal" : "Normal, saving current")}: " +
                $"{toPersist.Width}x{toPersist.Height} @ ({toPersist.Position.X},{toPersist.Position.Y})");
            _windowState.Persist(current, isMaximized);
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

        // 崩溃恢复：正常退出时清理恢复文件
        if (Document.HasDocument && !string.IsNullOrEmpty(Document.FilePath))
            RecoveryService.Cleanup(Document.FilePath);

        // 强制退出整个进程
        Environment.Exit(0);
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
    
}
