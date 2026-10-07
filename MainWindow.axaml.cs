using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LabelAva.Services;
using LabelAva.Models;
using LabelAva.Views;
using LabelAva.ViewModels;


namespace LabelAva;



public partial class MainWindow : Window
{
    // 快捷键设置
    private readonly AppSettingsProvider _settingsProvider = new();
    private ShortcutRouter _shortcutRouter = null!;
    
    // 编辑模式相关
    private TextBox? _translationTextBox;
    private DispatcherTimer? _recoveryDebounce;
    private Border? _editPanel;
    
    // UI 锁：防止命令执行时触发 UI 事件污染历史栈
    private bool _isUpdatingUI = false;
    
    // 主动失焦标志：Ctrl+Enter 提交时置 true，GotFocus 拦截器据此踢走焦点
    private bool _isIntentionalBlur = false;
    
    // 树视图拖拽交互：状态机与全部字段都搬进了 TreeDragDropController，
    // 这里只保留引用；在 InitializeAsync 里、ViewModel 就绪之后创建。
    private TreeDragDropController? _treeDragDrop;
    
    // 选中项同步防重入标志
    private bool _isSyncingSelection = false;
    
    // 选中项来源标志：true 表示选中变更由画布交互触发，需跳过视野居中
    private bool _isSelectionFromCanvas = false;
    
    // 键盘导航标志：true 表示选中变更由快捷键（Up/Down/PageUp/PageDown）触发，强制自动聚焦
    private bool _isKeyboardNavigation = false;

    private TranslationTreeItem? _subscribedTranslationItem;

    private bool _isHandlingCtrlEnter;

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

    // 窗口尺寸/位置的状态与持久化策略都在 WindowStateTracker 里；
    // 这里只保留视图侧的防抖定时器（何时触发属于视时序）。
    private readonly WindowStateTracker _windowState;
    private DispatcherTimer? _sizeDebounce;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Resources["DligFontFamily"] = null;
        Resources["DligFontFeatures"] = null;

        // 恢复上次窗口尺寸/位置（不立即最大化，让 OS 先记录 Normal 尺寸）
        _settingsProvider.Load();
        _windowState = new WindowStateTracker(_settingsProvider);

        var screenBounds = Screens?.All.Select(screen => screen.Bounds).ToList() ?? new List<PixelRect>();
        if (_windowState.HasUsableSavedPosition(screenBounds))
        {
            Position = _windowState.NormalBounds.Position;
            Width = _windowState.NormalBounds.Width;
            Height = _windowState.NormalBounds.Height;
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

        if (changes.HasFlag(SettingsChangeKind.CanvasMouse))
        {
            CanvasControl.UpdateSettings(settings);
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
    
    /// <summary>画布标注的右键菜单实例（由 OnCanvasLabelContextMenuRequested 构建与弹出）。</summary>
    private ContextMenu? _canvasContextMenu;

}