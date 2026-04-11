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
using LabelAva.Commands;
using System.Linq;
using LabelAva.ViewModels;


namespace LabelAva;



public partial class MainWindow : Window
{
    // 当前图片
    private Bitmap? _currentImage;
    private string? _currentImagePath;
    
    // 翻译数据已迁入 DocumentViewModel（通过 Document.TranslationData 访问）
    
    // 矩阵变换（_transformMatrix 已迁入 CanvasViewModel，通过 CanvasVM.TransformMatrix 访问）
    private MatrixTransform? _matrixTransform;
    
    // 拖动状态（_isPanning/_lastPanPoint 已迁入 CanvasViewModel）

    // 标注拖拽状态
    private bool _isDraggingLabel = false;
    private Border? _draggedLabel;
    private Point _labelDragLastPoint;
    
    // 快捷键设置
    private ShortcutSettings _shortcutSettings;
    
    // 首次加载标志
    private bool _isFirstImageLoaded = false;
    
    // 标注控件列表
    private List<Control> _labelControls = new();
    
    // // 状态栏
    // private TextBlock? _statusText;
    // private Border? _statusBar;
    // private TextBlock? _zoomText;
    
    // // 状态栏
    // private StatusBarViewModel.StatusType _currentStatusBarViewModel.StatusType = StatusBarViewModel.StatusType.Default;
    // private int _statusMessageId = 0; // 用于追踪最新的消息ID，防止并发覆盖
    
    // 编辑模式相关
    private bool _isProgrammaticTextChange = false; // 程序化设置文本时的标志
    private TextBox? _translationTextBox;
    private Border? _editPanel;
    
    // Dirty State 已迁入 DocumentViewModel（通过 Document.IsDirty 访问）
    
    // UI 锁：防止命令执行时触发 UI 事件污染历史栈
    private bool _isUpdatingUI = false;
    

    
    // 拖拽交互状态（数据模型坐标）
    private double _dragStartNormX = 0;
    private double _dragStartNormY = 0;
    private LabelItem? _draggingLabelItem;
    

    // 树视图拖拽交互状态
    private Point _treeDragStartPoint;
    private bool _isTreeItemDragging = false;
    private TranslationTreeItem? _draggedTreeItem;
    
    // 选中项同步防重入标志
    private bool _isSyncingSelection = false;

    // 分组单选按钮（Avalonia 自动生成 x:Name 字段）
    
    public MainWindowViewModel ViewModel => ((MainWindowViewModel)DataContext!);
    public StatusBarViewModel StatusBar => ViewModel.StatusBar;
    public EditViewModel Edit => ViewModel.Edit;
    public DocumentViewModel Document => ViewModel.Document;
    public NavigationViewModel Navigation => ViewModel.Navigation;
    public CanvasViewModel CanvasVM => ViewModel.CanvasVM;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        // 加载快捷键设置
        _shortcutSettings = ShortcutSettingsService.Load();
        
        // 初始化分组按钮快捷键提示
        UpdateGroupButtonsShortcutTips();
        
        // 订阅快捷键设置更改事件
        PreferencesWindow.SettingsChanged += OnShortcutSettingsChanged;
        
        // 获取状态栏控件引用
        // _statusText = this.FindControl<TextBlock>("_StatusText");
        StatusBar.UpdateStatus("就绪", StatusBarViewModel.StatusType.Success);
        // _statusBar = this.FindControl<Border>("_StatusBar");
        // _zoomText = this.FindControl<TextBlock>("_ZoomText");
        StatusBar.UpdateZoom(100);
        
        // 获取编辑面板控件引用
        _translationTextBox = this.FindControl<TextBox>("TranslationTextBox");
        _editPanel = this.FindControl<Border>("EditPanel");

        // 订阅文本框失去焦点事件（用于实现命令模式的文本编辑）
        if (_translationTextBox != null)
        {
            _translationTextBox.LostFocus += OnTranslationTextBoxLostFocus;
        }
        
        // // 添加默认 status-bar 类
        // if (_statusBar != null)
        // {
        //     _statusBar.Classes.Add("status-bar");
        // }
        
        // // 初始状态
        // if (_statusText != null)
        //     _statusText.Text = "就绪";
        // if (_zoomText != null)
        //     _zoomText.Text = "缩放: 100%";
        
        // 获取 MatrixTransform 引用（应用于包装 Grid）
        _matrixTransform = ImageWrapper.RenderTransform as MatrixTransform;
        if (_matrixTransform == null)
        {
            _matrixTransform = new MatrixTransform();
            ImageWrapper.RenderTransform = _matrixTransform;
        }
        
        // 树视图 ItemsSource 已通过 XAML 绑定 Navigation.TreeItems

        // 初始化树视图拖放事件（仅用于 DragOver/Drop，Pointer 事件在 DataTemplate 中处理）
        DragDrop.SetAllowDrop(ImageTreeView, true);
        ImageTreeView.AddHandler(DragDrop.DragOverEvent, OnTreeViewDragOver);
        ImageTreeView.AddHandler(DragDrop.DropEvent, OnTreeViewDrop);

        // 订阅容器尺寸变化事件
        ImageContainer.SizeChanged += OnImageContainerSizeChanged;
        
        // 订阅窗口关闭事件，确保清理资源
        this.Closing += OnWindowClosing;
        
        // 订阅鼠标按键事件（用于处理鼠标侧键快捷键）
        this.PointerPressed += OnMainWindowPointerPressed;
        
        // 初始化历史记录管理器（通过 HistoryViewModel 封装）
        var historyManager = new HistoryManager();
        ViewModel.History = new HistoryViewModel(historyManager, CommitCurrentEdit, StatusBar);
        ViewModel.History.HistoryStateChanged += OnHistoryStateChanged;
        
        // 初始化画布视图模型（合并原 ImageViewportViewModel + 标签操作）
        ViewModel.CanvasVM = new CanvasViewModel(ViewModel.History, StatusBar, CommitCurrentEdit);
        ViewModel.CanvasVM.TransformChanged += OnCanvasTransformChanged;
        
        // 初始化编辑视图模型（仅编辑模式状态，标签操作已迁入 CanvasViewModel）
        ViewModel.Edit = new EditViewModel(StatusBar);
        ViewModel.Edit.EditModeChanged += OnEditModeChanged;
        ViewModel.Edit.GroupChanged += OnGroupChanged;
        
        // 初始化导航视图模型
        ViewModel.Navigation = new NavigationViewModel(StatusBar);
        ViewModel.Navigation.CurrentImageChanged += OnNavigationCurrentImageChanged;
        ViewModel.Navigation.SelectedItemChanged += OnNavigationSelectedItemChanged;
        
        // 初始化文档视图模型
        var fileService = new FileDialogService(() => GetTopLevel(this));
        ViewModel.Document = new DocumentViewModel(
            fileService,
            ViewModel.History,
            StatusBar,
            ShowUnsavedChangesDialogAsync,
            ShowImageSelectionDialogAsync
        );
        ViewModel.Document.DocumentOpened += OnDocumentOpened;
        ViewModel.Document.DocumentClosed += OnDocumentClosed;

        // 订阅画布变换矩阵变更事件（同步矩阵到 UI 控件 + 状态栏 + FitScale）
        // （已在上方 CanvasVM 初始化时订阅）

        // 【新增】注册全局快捷键隧道拦截，在控件捕获前优先接管撤销/重做
        // Ctrl+Enter 提交功能也在这里处理（兼容主键盘 Return 和数字小键盘 Enter）
        this.AddHandler(InputElement.KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
    }
    
    
    /// <summary>
    /// 处理快捷键设置更改事件
    /// </summary>
    private void OnShortcutSettingsChanged(object? sender, ShortcutSettings settings)
    {
        _shortcutSettings = settings;
        
        // 更新分组切换按钮的快捷键提示
        UpdateGroupButtonsShortcutTips();
        
        StatusBar.UpdateStatus("快捷键设置已更新", StatusBarViewModel.StatusType.Success);
    }
    
    /// <summary>
    /// 更新分组切换按钮的快捷键提示
    /// </summary>
    private void UpdateGroupButtonsShortcutTips()
    {
        if (Group0RadioButton != null)
        {
            var shortcutText = ShortcutSettings.KeyGestureToString(_shortcutSettings.ToggleGroup0);
            ToolTip.SetTip(Group0RadioButton, $"切换到框内 ({shortcutText})");
        }
        
        if (Group1RadioButton != null)
        {
            var shortcutText = ShortcutSettings.KeyGestureToString(_shortcutSettings.ToggleGroup1);
            ToolTip.SetTip(Group1RadioButton, $"切换到框外 ({shortcutText})");
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
        ImageContainer.SizeChanged -= OnImageContainerSizeChanged;
        this.Closing -= OnWindowClosing;
        
        // 清空历史记录
        ViewModel.History.Clear();
        
        // 释放图片资源
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        
        // 清除标注控件（使用辅助方法解绑事件）
        ClearLabelControls();
        
        // 清空树视图数据
        Navigation.TreeItems.Clear();
        
        // 清空其他数据（TranslationData 由 DocumentViewModel 管理，无需手动清理）
        Navigation.ImageFolderPath = null;
        Navigation.ImageNames.Clear();
        
        // 重置状态
        _isFirstImageLoaded = false;
        
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
            Title = "未保存的更改",
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10
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
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;

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
    /// DocumentViewModel.DocumentOpened 事件处理
    /// </summary>
    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        // 初始化导航（TranslationData 由 DocumentViewModel 管理，通过 Document.TranslationData 访问）
        Navigation.InitializeNavigation(e.ImageFolderPath, e.ImageNames);

        if (Navigation.ImageNames.Count > 0)
        {
            Navigation.CurrentImageIndex = 0;
            LoadCurrentImage();
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
        // 清理图片和标注
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        _currentImagePath = null;
        CanvasVM.ResetTransform();
        CanvasVM.UpdateImageSize(new Size(0, 0));

        ClearLabelControls();

        // 清理导航状态（TranslationData 由 DocumentViewModel 管理）
        Navigation.ClearNavigation();
        _isFirstImageLoaded = false;

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
        System.Diagnostics.Debug.WriteLine($"[DEBUG] OnNavigationSelectedItemChanged: _isSyncingSelection={_isSyncingSelection}, Nav.SelectedItem={Navigation.SelectedItem}, TreeView.SelectedItem={ImageTreeView.SelectedItem}");
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
    
    /// <summary>
    /// 图像容器尺寸变化时重新应用边界限制
    /// </summary>
    private void OnImageContainerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_currentImage == null) return;
        
        // 通知 Viewport 容器尺寸变化，重新应用边界限制
        CanvasVM.UpdateContainerSize(new Size(e.NewSize.Width, e.NewSize.Height));
        CanvasVM.OnContainerSizeChanged();
    }
    
    /// <summary>
    /// CanvasViewModel.TransformChanged 事件处理
    /// 同步矩阵到 UI 控件 + 状态栏缩放百分比 + FitScale 到树视图项
    /// </summary>
    private void OnCanvasTransformChanged(object? sender, EventArgs e)
    {
        // 同步矩阵到 UI 控件
        ApplyTransform();
        // 同步缩放百分比到状态栏
        StatusBar.UpdateZoom(CanvasVM.ZoomPercent);
        // 同步 FitScale 到树视图项
        SaveCurrentFitScale(CanvasVM.CurrentFitScale);
    }
    
    /// <summary>
    /// 应用变换矩阵到 Image 控件（从 CanvasVM 同步到 UI）
    /// </summary>
    private void ApplyTransform()
    {
        if (_matrixTransform != null)
        {
            _matrixTransform.Matrix = CanvasVM.TransformMatrix;
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
    
    private void OnPreferences(object? sender, RoutedEventArgs e)
    {
        var preferencesWindow = new Views.PreferencesWindow();

        // 订阅设置变更事件
        Views.PreferencesWindow.SettingsChanged += OnSettingsChanged;

        preferencesWindow.Closed += (s, args) =>
        {
            Views.PreferencesWindow.SettingsChanged -= OnSettingsChanged;
        };

        preferencesWindow.Show();
    }

    /// <summary>
    /// 设置变更事件处理
    /// </summary>
    private void OnSettingsChanged(object? sender, ShortcutSettings settings)
    {
        // 清除颜色缓存以应用新颜色
        GroupIndexToBrushConverter.ClearCache();

        // 刷新分组按钮颜色
        UpdateGroupButtonColors();

        // 如果有选中的标签，刷新高亮颜色
        if (Navigation.SelectedItem is TranslationTreeItem selectedItem)
        {
            HighlightLabel(selectedItem.Index);
        }

        // 刷新树状视图以应用新的分组颜色
        RefreshTreeView();
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
        Document.SetDirty(true);

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
        if (CanvasVM.PendingNewLabelIndex.HasValue)
        {
            previouslySelectedLabelIndex = CanvasVM.PendingNewLabelIndex;
        }
        
        // 重新构建树视图
        Navigation.BuildTreeView(Document.TranslationData);

        // 不清空 TextBox，保留用户正在编辑的内容
        // TextBox 的值会在后续的 SelectionChanged 中被正确设置

        // ======================= FIX START =======================
        // 更新画布标注
        // 【修复核心】：如果当前正在按下鼠标拖拽某个标签，则跳过全量图形销毁与重建，
        // 保护当前正在捕获鼠标事件的原生控件不被销毁，从而保持拖拽的连续性。
        if (!_isDraggingLabel)
        {
            UpdateLabels();
        }
        else
        {
            // 如果跳过重建，也要确保同步其可能因选中项变化带来的高亮状态
            if (previouslySelectedLabelIndex.HasValue)
            {
                HighlightLabel(previouslySelectedLabelIndex.Value);
            }
        }
        // ======================= FIX END =======================
        
        // 尝试恢复当前选中的图片
        if (!string.IsNullOrEmpty(_currentImagePath))
        {
            var imageName = Path.GetFileName(_currentImagePath);
            var treeItem = Navigation.TreeItems.FirstOrDefault(t => t.ImageName == imageName);
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
        if (CanvasVM.PendingNewLabelIndex.HasValue && _shortcutSettings.AutoFocusTextBox)
        {
            // 清除待选中状态后，聚焦到文本框
            CanvasVM.ClearPendingNewLabelIndex();

            // 延迟聚焦到文本框，确保 UI 已完成重建
            Dispatcher.UIThread.Post(() =>
            {
                _translationTextBox?.Focus();
            }, DispatcherPriority.Loaded);
        }
        else if (CanvasVM.PendingNewLabelIndex.HasValue)
        {
            // 如果不需要自动聚焦，仅清除待选中状态
            CanvasVM.ClearPendingNewLabelIndex();
        }
        
    }
    
    /// <summary>
    /// 编辑模式变更事件处理（由 EditViewModel.EditModeChanged 触发）
    /// </summary>
    private void OnEditModeChanged(object? sender, EventArgs e)
    {
        // 更新分组按钮颜色（仍需 code-behind，因为涉及 Window Resources）
        if (Edit.IsEditMode)
        {
            UpdateGroupButtonColors();
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
    }

    /// <summary>
    /// 快捷输入按钮点击事件（占位处理函数）
    /// 后续可在此实现插入预设文本或特殊符号的功能
    /// </summary>
    private void OnQuickInputButtonClick(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现快捷输入功能
        if (sender is Button button)
        {
            StatusBar.UpdateStatus($"快捷输入按钮 '{button.Content}' 被点击", StatusBarViewModel.StatusType.Info);
        }
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
            UpdateGroupButtonColors();
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
            var settings = ShortcutSettingsService.Load();

            // Group0 (框内) - 分组索引为1
            var group0ColorHex = settings.Colors.GroupColors.GetValueOrDefault(1, "#FFE6E6");
            var group0Color = Avalonia.Media.Color.Parse(group0ColorHex);
            var group0HoverColor = AdjustBrightness(group0Color, 1.2); // 增加亮度
            var group0PressedColor = AdjustBrightness(group0Color, 0.7); // 减少亮度

            // Group1 (框外) - 分组索引为2
            var group1ColorHex = settings.Colors.GroupColors.GetValueOrDefault(2, "#E6E6FF");
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

    // 存储每个按钮的颜色信息用于hover/pressed
    private readonly Dictionary<RadioButton, (Color normal, Color hover, Color pressed, Color normalBorder, Color activated)> _buttonColors = new();

    /// <summary>
    /// 为RadioButton应用颜色（通过样式类）
    /// </summary>
    private void ApplyRadioButtonColors(RadioButton rb, Avalonia.Media.Color normalColor, Avalonia.Media.Color hoverColor, Avalonia.Media.Color pressedColor)
    {
        // 存储颜色信息
        var activatedColor = AdjustBrightness(normalColor, 1.1);
        _buttonColors[rb] = (normalColor, hoverColor, pressedColor, Avalonia.Media.Color.Parse("#CCCCCC"), Avalonia.Media.Color.Parse("#333333"));

        // 订阅事件
        rb.PointerEntered -= OnRadioButtonPointerEntered;
        rb.PointerExited -= OnRadioButtonPointerExited;
        rb.PointerPressed -= OnRadioButtonPointerPressed;
        rb.PointerReleased -= OnRadioButtonPointerReleased;

        rb.PointerEntered += OnRadioButtonPointerEntered;
        rb.PointerExited += OnRadioButtonPointerExited;
        rb.PointerPressed += OnRadioButtonPointerPressed;
        rb.PointerReleased += OnRadioButtonPointerReleased;
    }

    /// <summary>
    /// RadioButton悬停事件
    /// </summary>
    private void OnRadioButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is RadioButton rb && _buttonColors.TryGetValue(rb, out var colors))
        {
            if (rb.IsChecked == true)
            {
                rb.Background = new Avalonia.Media.SolidColorBrush(AdjustBrightness(colors.activated, 1.15));
            }
            else
            {
                rb.Background = new Avalonia.Media.SolidColorBrush(colors.hover);
            }
        }
    }

    /// <summary>
    /// RadioButton离开事件
    /// </summary>
    private void OnRadioButtonPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is RadioButton rb && _buttonColors.TryGetValue(rb, out var colors))
        {
            if (rb.IsChecked == true)
            {
                rb.Background = new Avalonia.Media.SolidColorBrush(colors.activated);
            }
            else
            {
                rb.Background = new Avalonia.Media.SolidColorBrush(colors.normal);
            }
        }
    }

    /// <summary>
    /// RadioButton按下事件
    /// </summary>
    private void OnRadioButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is RadioButton rb && _buttonColors.TryGetValue(rb, out var colors))
        {
            rb.Background = new Avalonia.Media.SolidColorBrush(colors.pressed);
        }
    }

    /// <summary>
    /// RadioButton释放事件
    /// </summary>
    private void OnRadioButtonPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is RadioButton rb && _buttonColors.TryGetValue(rb, out var colors))
        {
            if (rb.IsChecked == true)
            {
                rb.Background = new Avalonia.Media.SolidColorBrush(colors.activated);
            }
            else
            {
                rb.Background = new Avalonia.Media.SolidColorBrush(colors.hover);
            }
        }
    }

    /// <summary>
    /// 激活RadioButton（选中状态）
    /// </summary>
    private void ActivateRadioButton(RadioButton rb, Avalonia.Media.Color color)
    {
        rb.Background = new Avalonia.Media.SolidColorBrush(color);
        rb.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
        rb.BorderThickness = new Avalonia.Thickness(2);
    }

    /// <summary>
    /// 停用RadioButton（未选中状态）
    /// </summary>
    private void DeactivateRadioButton(RadioButton rb, Avalonia.Media.Color normalColor)
    {
        rb.Background = new Avalonia.Media.SolidColorBrush(normalColor);
        rb.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"));
        rb.BorderThickness = new Avalonia.Thickness(1);
    }
    
    /// <summary>
    /// 当文本框内容修改时，同步到树状图节点（禁止直接修改底层数据）
    /// 底层数据的修改必须通过 ChangeTextCommand 进行
    /// </summary>
    private void OnTranslationTextChanged(object? sender, TextChangedEventArgs e)
    {
        // 如果是程序化设置文本，则仅设置光标位置并返回
        if (_isProgrammaticTextChange)
        {
            if (!string.IsNullOrEmpty(_translationTextBox?.Text))
            {
                var len = _translationTextBox.Text.Length;
                _translationTextBox.CaretIndex = len;
                _translationTextBox.SelectionStart = len;
                _translationTextBox.SelectionEnd = len;
            }
            return;
        }

        // 在 UI 重建期间，忽略文本改变事件
        if (_isUpdatingUI || !Edit.IsEditMode || _translationTextBox == null) return;

        if (Navigation.SelectedItem is TranslationTreeItem selectedTreeItem)
        {
            // 仅同步修改树节点的显示文本（注意：这里不修改底层数据模型，底层修改由 Commit 负责）
            selectedTreeItem.Text = _translationTextBox.Text ?? string.Empty;
        }
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
        // 如果没有处于编辑模式，或者没有焦点/数据，直接返回
        if (!Edit.IsEditMode || _translationTextBox == null || Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;

        // 只有当前树状图选中的是文本节点时，才需要结算
        if (Navigation.SelectedItem is TranslationTreeItem currentTreeItem)
        {
            string imageName = Path.GetFileName(_currentImagePath);
            if (Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            {
                var labelItem = labels.FirstOrDefault(l => l.TextIndex == currentTreeItem.Index);
                if (labelItem != null)
                {
                    string newText = _translationTextBox.Text ?? string.Empty;
                    string oldText = labelItem.Text ?? string.Empty;

                    // 只有发生了实质性修改，才推入历史栈
                    // 注意：如果文本没有变化，就不会触发 HistoryChanged，避免 RebuildCurrentView 清空 TextBox
                    if (oldText != newText)
                    {
                        var command = new ChangeTextCommand(labelItem, oldText, newText);
                        ViewModel.History.ExecuteCommand(command);
                    }
                }
            }
        }
    }


    /// <summary>
    /// 全局快捷键拦截（隧道路由，在子控件处理前触发）
    /// 用于接管并统一处理 TextBox 等子控件的撤销/重做快捷键冲突
    /// </summary>
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        // 兼容 Windows (Control) 和 Mac (Meta)
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;
        bool isShiftPressed = (modifiers & KeyModifiers.Shift) != 0;

        // 【新增修复】：优先处理 Ctrl+Enter 提交并失焦
        // 兼容主键盘 Return 和数字小键盘 Enter
        if (isCtrlPressed && (e.Key == Key.Return || e.Key == Key.Enter))
        {
            // 检查 TextBox 是否有焦点
            if (_translationTextBox != null && _translationTextBox.IsFocused)
            {
                CommitCurrentEdit();
                e.Handled = true;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // 获取顶级窗口并强制清空焦点管理器中的当前焦点
                    var topLevel = TopLevel.GetTopLevel(this);
                    topLevel?.FocusManager?.ClearFocus();
                });
                
                return;
            }
        }

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
        
        // 创建当前按键的 KeyGesture 用于比较（与树图导航相同的处理方式）
        var currentGesture = new KeyGesture(e.Key, e.KeyModifiers);

        // 检查 TextBox 是否有焦点 - 如果有焦点，则不处理分组切换快捷键
        // 避免在编辑文本时意外触发分组切换
        bool isTextBoxFocused = _translationTextBox != null && _translationTextBox.IsFocused;

        // 检查是否匹配分组切换快捷键（仅当 TextBox 无焦点时处理）
        if (!isTextBoxFocused)
        {
            if (_shortcutSettings.ToggleGroup0 != null &&
                currentGesture.Equals(_shortcutSettings.ToggleGroup0))
            {
                SwitchToGroup(0);
                e.Handled = true;
            }
            else if (_shortcutSettings.ToggleGroup1 != null &&
                     currentGesture.Equals(_shortcutSettings.ToggleGroup1))
            {
                SwitchToGroup(1);
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// 切换到指定分组
    /// </summary>
    private void SwitchToGroup(int groupIndex)
    {
        Edit.SwitchGroupCommand.Execute(groupIndex);
        // 同步 RadioButton 选中状态
        if (groupIndex == 0 && Group0RadioButton != null)
            Group0RadioButton.IsChecked = true;
        else if (groupIndex == 1 && Group1RadioButton != null)
            Group1RadioButton.IsChecked = true;
    }


    /// <summary>
    /// 清除所有标注控件并解绑事件（防止内存泄漏和幽灵点击）
    /// </summary>
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
    
    
    
    /// <summary>
    /// 处理标签标记的按下事件（选中及准备拖拽）
    /// </summary>
    private void OnLabelMarkerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 仅在编辑模式下允许拖拽标签
        if (!Edit.IsEditMode)
            return;

        // 从Tag中提取labelIndex（支持新的ValueTuple格式和旧的int格式）
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
            CommitCurrentEdit();

            // 移除 TextBox 的焦点，将焦点设置到窗口上，确保鼠标事件能正确路由到标记
            this.Focus();

            // 先记录拖拽开始时的数据模型坐标并设置拖动状态（在 SelectLabelByIndex 之前）
            if (Document.TranslationData != null && _currentImagePath != null)
            {
                string imageName = Path.GetFileName(_currentImagePath);
                if (Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
                {
                    var labelItem = labels.FirstOrDefault(l => l.TextIndex == labelIndex);
                    if (labelItem != null)
                    {
                        _dragStartNormX = labelItem.X;
                        _dragStartNormY = labelItem.Y;
                        _draggingLabelItem = labelItem;
                    }
                }
            }

            _isDraggingLabel = true;
            _draggedLabel = border;
            _labelDragLastPoint = point.Position;
            e.Pointer.Capture(border);

            // 点击立即选中（使其高亮聚焦）
            SelectLabelByIndex(labelIndex.Value);
        }
    }
    
    /// <summary>
    /// 处理标签标记的移动事件（执行拖拽）
    /// </summary>
    private void OnLabelMarkerPointerMoved(object? sender, PointerEventArgs e)
    {
        // 仅在编辑模式下允许拖拽标签
        if (!Edit.IsEditMode)
            return;
        
        if (_isDraggingLabel && _draggedLabel != null && sender == _draggedLabel)
        {
            e.Handled = true;
            
            var currentPoint = e.GetPosition(ImageWrapper);
            var delta = currentPoint - _labelDragLastPoint;
            
            double currentLeft = Canvas.GetLeft(_draggedLabel);
            double currentTop = Canvas.GetTop(_draggedLabel);
            
            double newLeft = currentLeft + delta.X;
            double newTop = currentTop + delta.Y;
            
            // 限制在图片范围内 (防止拖拽出界)
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
            
            // 实时更新底层数据模型坐标
            UpdateDraggedLabelData(_draggedLabel, newLeft, newTop);
        }
    }
    
    /// <summary>
    /// 处理标签标记的释放事件（结束拖拽）
    /// </summary>
    private void OnLabelMarkerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 仅在编辑模式下允许拖拽标签
        if (!Edit.IsEditMode)
            return;
        
        if (_isDraggingLabel && _draggedLabel != null && sender == _draggedLabel)
        {
            var point = e.GetCurrentPoint(ImageWrapper);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                e.Handled = true;
                
                // 最终更新一次坐标信息
                double currentLeft = Canvas.GetLeft(_draggedLabel);
                double currentTop = Canvas.GetTop(_draggedLabel);
                UpdateDraggedLabelData(_draggedLabel, currentLeft, currentTop);
                
                // 检查是否真的移动了（使用新的交互状态字段）
                if (_draggingLabelItem != null)
                {
                    double currentX = _draggingLabelItem.X;
                    double currentY = _draggingLabelItem.Y;
                    
                    if (Math.Abs(currentX - _dragStartNormX) > 0.0001 || Math.Abs(currentY - _dragStartNormY) > 0.0001)
                    {
                        // 真的移动了，创建 MoveLabelCommand
                        var command = new MoveLabelCommand(_draggingLabelItem, _dragStartNormX, _dragStartNormY, currentX, currentY);
                        ViewModel.History.ExecuteCommand(command);
                    }
                }
                
                // 清理拖拽状态
                _draggingLabelItem = null;
                _isDraggingLabel = false;
                _draggedLabel = null;
                e.Pointer.Capture(null);
            }
        }
    }
    
    /// <summary>
    /// 更新被拖拽标签的底层归一化坐标数据
    /// </summary>
    private void UpdateDraggedLabelData(Border labelBorder, double left, double top)
    {
        if (labelBorder.Tag is not int labelIndex || _currentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;

        string imageName = Path.GetFileName(_currentImagePath);
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            return;

        var labelItem = labels.FirstOrDefault(l => l.TextIndex == labelIndex);
        if (labelItem != null)
        {
            // 恢复中心点坐标并将其归一化 (0.0 ~ 1.0)
            double centerX = left + 32;
            double centerY = top + 32;
            
            labelItem.X = centerX / _currentImage.Size.Width;
            labelItem.Y = centerY / _currentImage.Size.Height;
        }
    }
    
    /// <summary>
    /// 根据索引选中文本节点并在树视图中聚焦
    /// </summary>
    private void SelectLabelByIndex(int labelIndex)
    {
        if (Navigation.CurrentTreeItem == null) return;
        
        // 在当前图片的翻译列表中查找对应索引的项
        var translationItem = Navigation.CurrentTreeItem.Translations.FirstOrDefault(t => t.Index == labelIndex);
        
        if (translationItem != null)
        {
            // 选中树视图中的节点
            Navigation.SelectedItem = translationItem;
            
            // 确保树节点已展开
            Navigation.CurrentTreeItem.IsExpanded = true;
            
            // 聚焦到树视图，以便键盘导航
            ImageTreeView.Focus();
            
            StatusBar.UpdateStatus($"已选中标注 #{labelIndex}", StatusBarViewModel.StatusType.Info);
        }
    }
    
    /// <summary>
    /// 在指定坐标新建标签（如果该位置没有现有标签）
    /// </summary>
    private void AddNewLabel(double imageX, double imageY)
    {
        if (_currentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath) || Navigation.CurrentTreeItem == null)
            return;

        string imageName = Path.GetFileName(_currentImagePath);
        
        // 确保字典中有该图片的数据列表
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
        {
            labels = new List<LabelItem>();
            Document.TranslationData.ImageLabels[imageName] = labels;
        }

        // 计算新的编号 (TextIndex)，取当前最大值 + 1
        int nextIndex = labels.Any() ? labels.Max(l => l.TextIndex) + 1 : 1;

        // 计算归一化坐标 (0.0 ~ 1.0)
        double normX = imageX / _currentImage.Size.Width;
        double normY = imageY / _currentImage.Size.Height;

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
        CanvasVM.AddLabel(labels, newLabel, nextIndex);

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
    
    private void OnAddLabel(object? sender, RoutedEventArgs e)
    {
        StatusBar.UpdateStatus("添加标注模式");
    }
    
    private void OnDeleteLabel(object? sender, RoutedEventArgs e)
    {
        StatusBar.UpdateStatus("删除标注模式");
    }
    
    private void OnClearCanvas(object? sender, RoutedEventArgs e)
    {
        if (_currentImage != null)
        {
            _currentImage.Dispose();
            _currentImage = null;
        }
        _currentImagePath = null;
        CanvasVM.ResetTransform();
        CanvasVM.UpdateImageSize(new Size(0, 0));
        
        // 清除标注（使用辅助方法解绑事件）
        ClearLabelControls();
        
        StatusBar.UpdateStatus("画布已清空");
    }
    
    // OnZoomIn/OnZoomOut/OnResetZoom 已迁入 CanvasViewModel（通过 Command 绑定）
    
    private void OnResetZoom(object? sender, RoutedEventArgs e)
    {
        // 保留此事件处理以兼容 XAML Click 绑定过渡期
        CanvasVM.ResetZoomCommand.Execute(null);
    }
    
    private void OnAbout(object? sender, RoutedEventArgs e)
    {
        StatusBar.UpdateStatus("LabelAva 1.0");
    }
    
    // ==================== ImageContainer 事件处理 ====================
    
    private void OnImageContainerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(ImageContainer);
        
        // 左键行为取决于是否在编辑模式
        if (point.Properties.IsLeftButtonPressed)
        {
            if (Edit.IsEditMode)
            {
                // ========== 编辑模式：点击添加标签 ==========
                // 获取相对于原图(MainImage)的实际像素坐标
                var imagePoint = e.GetPosition(MainImage);
                
                // 确保点击在图片范围内才添加
                if (_currentImage != null && 
                    imagePoint.X >= 0 && imagePoint.X <= _currentImage.Size.Width &&
                    imagePoint.Y >= 0 && imagePoint.Y <= _currentImage.Size.Height)
                {
                    // 检查点击位置是否已有标签，如果有则选中，没有则创建
                    int? existingLabelIndex = FindLabelAtPosition(imagePoint.X, imagePoint.Y);
                    
                    if (existingLabelIndex.HasValue)
                    {
                        // 选中现有标签前，先提交当前正在编辑的文本
                        CommitCurrentEdit();
                        SelectLabelByIndex(existingLabelIndex.Value);
                    }
                    else
                    {
                        // 创建新标签前，先提交当前正在编辑的文本
                        CommitCurrentEdit();
                        AddNewLabel(imagePoint.X, imagePoint.Y);
                    }
                }
                e.Handled = true;
            }
            else
            {
                // ========== 浏览模式：左键平移 ==========
                CanvasVM.StartPan(e.GetPosition(ImageContainer));
                ImageContainer.Cursor = new Cursor(StandardCursorType.Hand);
                e.Handled = true;
            }
        }
        // 允许任何模式下使用中键或右键平移
        else if (point.Properties.IsMiddleButtonPressed || point.Properties.IsRightButtonPressed)
        {
            CanvasVM.StartPan(e.GetPosition(ImageContainer));
            ImageContainer.Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// 检查指定像素坐标是否靠近现有标签
    /// </summary>
    private int? FindLabelAtPosition(double x, double y)
    {
        if (_currentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return null;

        string imageName = Path.GetFileName(_currentImagePath);
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            return null;

        double imageWidth = _currentImage.Size.Width;
        double imageHeight = _currentImage.Size.Height;
        
        // 标注的大小是64x64，一半是32
        double halfSize = 32;
        
        foreach (var label in labels)
        {
            double labelX = label.X * imageWidth;
            double labelY = label.Y * imageHeight;
            
            // 检查点击位置是否在标签范围内
            if (x >= labelX - halfSize && x <= labelX + halfSize &&
                y >= labelY - halfSize && y <= labelY + halfSize)
            {
                return label.TextIndex;
            }
        }
        
        return null;
    }
    
    private void OnImageContainerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (CanvasVM.IsPanning)
        {
            CanvasVM.UpdatePan(e.GetPosition(ImageContainer));
        }
    }
    
    private void OnImageContainerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (CanvasVM.IsPanning)
        {
            CanvasVM.EndPan();
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
        
        // 以鼠标为中心进行缩放（委托给 CanvasVM）
        CanvasVM.ApplyZoomDelta(zoom, mousePos);
        
        e.Handled = true;
    }
    
    // ==================== 矩阵变换核心方法（已迁入 CanvasViewModel）====================

    /// <summary>
    /// 计算适应容器的初始变换（Fit模式）—— 委托给 CanvasViewModel
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
        
        // 通知 CanvasVM 容器和图片尺寸，然后计算 Fit 变换
        CanvasVM.UpdateContainerSize(new Size(containerBounds.Width, containerBounds.Height));
        CanvasVM.UpdateImageSize(new Size(_currentImage.Size.Width, _currentImage.Size.Height));
        CanvasVM.CalculateFitTransform();
    }
    
    /// <summary>
    /// 保存当前图片的 fit 缩放比例到对应的树视图项
    /// </summary>
    private void SaveCurrentFitScale(double fitScale)
    {
        if (_currentImagePath == null || Navigation.TreeItems.Count == 0) return;
        
        string imageName = Path.GetFileName(_currentImagePath);
        
        // 找到对应的 ImageTreeItem 并保存 fit 比例
        foreach (var item in Navigation.TreeItems)
        {
            if (item.ImageName == imageName)
            {
                item.FitScale = fitScale;
                Navigation.CurrentTreeItem = item;
                break;
            }
        }
    }
    
    // GetCurrentScale 已迁入 CanvasViewModel（通过 CanvasVM.ZoomPercent / 100 获取）
    
    // ==================== 图片加载 ====================

    /// <summary>
    /// 更新标注：根据当前图片的标注数据，在画布上显示编号
    /// </summary>
    private void UpdateLabels()
    {
        // 移除旧的标注（使用辅助方法解绑事件）
        ClearLabelControls();

        // 没有图片或翻译数据时返回
        if (_currentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;

        // 获取当前图片的文件名（作为键）
        string imageName = Path.GetFileName(_currentImagePath);
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            return;

        double imageWidth = _currentImage.Size.Width;
        double imageHeight = _currentImage.Size.Height;

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
                Classes = { "label-marker" }, // 添加样式类
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
            
            // 添加拖拽及点击事件
            border.PointerPressed += OnLabelMarkerPointerPressed;
            border.PointerMoved += OnLabelMarkerPointerMoved;
            border.PointerReleased += OnLabelMarkerPointerReleased;
            
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
        if (Navigation.SelectedItem is TranslationTreeItem selectedTranslation)
        {
            HighlightLabel(selectedTranslation.Index);
        }
    }
    
    /// <summary>
    /// 获取指定分组的背景颜色
    /// </summary>
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

    /// <summary>
    /// 获取当前设置中的选中高亮颜色
    /// </summary>
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

    /// <summary>
    /// 高亮显示指定编号的标注控件
    /// </summary>
    private void HighlightLabel(int labelIndex)
    {
        var selectedBrush = GetSelectedHighlightBrush();

        foreach (var control in _labelControls)
        {
            if (control is Border border)
            {
                // Tag 是一个元组 (TextIndex, GroupIndex)
                if (border.Tag is ValueTuple<int, int> tag && tag.Item1 == labelIndex)
                {
                    // 高亮状态：使用自定义颜色，加粗白边框，并提升图层显示层级
                    border.Background = selectedBrush;
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(3);
                    border.ZIndex = 20;
                }
                else if (border.Tag is ValueTuple<int, int> normalTag)
                {
                    // 普通状态：恢复对应分组的颜色
                    var groupIndex = normalTag.Item2;
                    border.Background = GetGroupBrush(groupIndex);
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                    border.ZIndex = 10;
                }
            }
        }
    }
    
    private void LoadCurrentImage()
    {
        if (Navigation.ImageNames.Count == 0 || string.IsNullOrEmpty(Navigation.ImageFolderPath))
            return;
        
        if (Navigation.CurrentImageIndex < 0 || Navigation.CurrentImageIndex >= Navigation.ImageNames.Count)
            return;
        
        var imageName = Navigation.ImageNames[Navigation.CurrentImageIndex];
        var imagePath = Path.Combine(Navigation.ImageFolderPath!, imageName);
        
        if (File.Exists(imagePath))
        {
            LoadImage(imagePath);
        }
        else
        {
            StatusBar.UpdateStatus($"图片文件不存在: {imagePath}", StatusBarViewModel.StatusType.Error);
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
            foreach (var item in Navigation.TreeItems)
            {
                if (item.ImageName == imageName)
                {
                    Navigation.CurrentTreeItem = item;
                    break;
                }
            }
            
            // 通知 CanvasVM 图片尺寸
            CanvasVM.UpdateImageSize(new Size(_currentImage.Size.Width, _currentImage.Size.Height));
            
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
                        // TransformChanged 事件会自动同步 ApplyTransform + StatusBar.UpdateZoom

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
                StatusBar.UpdateZoom(CanvasVM.ZoomPercent);
                // 更新标注显示
                UpdateLabels();
            }
            
            // 更新状态栏显示当前图片信息
            if (Navigation.ImageNames.Count > 0)
            {
                StatusBar.UpdateStatus($"[{Navigation.CurrentImageIndex + 1}/{Navigation.ImageNames.Count}] {Path.GetFileName(imagePath)}", StatusBarViewModel.StatusType.Info);
            }
            else
            {
                StatusBar.UpdateStatus($"已加载图片: {Path.GetFileName(imagePath)}", StatusBarViewModel.StatusType.Info);
            }
        }
        catch (Exception ex)
        {
            StatusBar.UpdateStatus($"加载图片失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
            // 发生异常时也要确保图片可见
            MainImage.IsVisible = true;
        }
    }
    
    // ==================== 辅助方法 ====================
    
    // GetZoomText 已迁入 CanvasViewModel（通过 CanvasVM.ZoomPercent 获取）
    
    // private void StatusBar.UpdateStatus(string message)
    // {
    //     StatusBar.UpdateStatus(message, StatusBarViewModel.StatusType.Default);
    // }
    
    // // 改为 async void
    // private async void StatusBar.UpdateStatus(string message, StatusBarViewModel.StatusType statusType)
    // {
    //     if (_statusText == null) return;
        
    //     _statusText.Text = message;
    //     _currentStatusBarViewModel.StatusType = statusType;
        
    //     // 根据状态类型设置样式
    //     if (_statusBar != null)
    //     {
    //         _statusBar.Classes.Set("status-success", statusType == StatusBarViewModel.StatusType.Success);
    //         _statusBar.Classes.Set("status-warn", statusType == StatusBarViewModel.StatusType.Warn);
    //         _statusBar.Classes.Set("status-error", statusType == StatusBarViewModel.StatusType.Error);
    //     }
        
    //     // 设置文字颜色
    //     if (_statusText != null)
    //     {
    //         _statusText.Classes.Set("status-success", statusType == StatusBarViewModel.StatusType.Success);
    //         _statusText.Classes.Set("status-warn", statusType == StatusBarViewModel.StatusType.Warn);
    //         _statusText.Classes.Set("status-error", statusType == StatusBarViewModel.StatusType.Error);
    //     }

    //     // 更新当前的 MessageId
    //     int currentId = ++_statusMessageId;
        
    //     if (statusType != StatusBarViewModel.StatusType.Default)
    //     {
    //         await Task.Delay(100);
            
    //         // 如果在等待期间没有任何新的状态更新，就触发回退机制还原为 Default
    //         if (currentId == _statusMessageId)
    //         {
    //             if (_statusBar != null)
    //             {
    //                 _statusBar.Classes.Set("status-success", false);
    //                 _statusBar.Classes.Set("status-warn", false);
    //                 _statusBar.Classes.Set("status-error", false);
    //             }
    //             if (_statusText != null)
    //             {
    //                 _statusText.Classes.Set("status-success", false);
    //                 _statusText.Classes.Set("status-warn", false);
    //                 _statusText.Classes.Set("status-error", false);
    //             }
    //             _currentStatusBarViewModel.StatusType = StatusBarViewModel.StatusType.Default;
    //         }
    //     }
    // }
    
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

        if (Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;

        string imageName = Path.GetFileName(_currentImagePath);

        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
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
        
        if (Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;
        
        string imageName = Path.GetFileName(_currentImagePath);
        
        // 获取当前图片的所有标签
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
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
    
    // BuildTreeView 已迁移到 NavigationViewModel.BuildTreeView()
    
    /// <summary>
    /// 树视图选择变更事件（处理图片切换 & 自动折叠展开）
    /// </summary>
    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedItem = ImageTreeView.SelectedItem;
        System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTreeViewSelectionChanged: _isSyncingSelection={_isSyncingSelection}, TreeView.SelectedItem={selectedItem}");
        
        // 同步选中项到 NavigationViewModel（防重入：仅在非同步期间更新 VM，避免循环触发）
        if (!_isSyncingSelection)
        {
            Navigation.SelectedItem = selectedItem;
        }
        
        // 重置文本框占位符
        if (_translationTextBox != null)
        {
            _translationTextBox.Watermark = "选中文本节点以编辑";
        }
        
        if (selectedItem == null) return;

        ImageTreeItem? targetRootItem = null;

        // 1. 判断选中项是父节点(图片)还是子节点(翻译文本)
        if (selectedItem is ImageTreeItem rootItem)
        {
            targetRootItem = rootItem;
        }
        else if (selectedItem is TranslationTreeItem childItem)
        {
            targetRootItem = Navigation.GetParentImageItem(childItem);
        }

        // 2. 图片切换（委托给 NavigationViewModel 判断）
        if (targetRootItem != null && Navigation.TrySwitchToImage(targetRootItem.ImageName))
        {
            LoadCurrentImage();
            CalculateFitTransform();
            // TransformChanged 事件会自动同步 ApplyTransform + StatusBar.UpdateZoom
            UpdateLabels();
        }

        // 2. 动态展开/收起逻辑 (手风琴效果)
        if (targetRootItem != null)
        {
            Navigation.ApplyAccordion(targetRootItem);
        }
        
        // 3. 高亮与视野居中处理
        if (selectedItem is TranslationTreeItem targetChildItem)
        {
            // 切换图片视图中的编号高亮
            HighlightLabel(targetChildItem.Index);
            
            double currentScale = CanvasVM.ZoomPercent / 100;
            double fitScale = targetRootItem?.FitScale ?? 1.0;
            
            // 如果当前缩放比例大于 fit 比例（用户手动放大了）
            if (currentScale > fitScale)
            {
                CenterOnLabel(targetChildItem.Index);
            }
            
            // ======== 将选中节点的文本写入编辑框 ========
            if (_translationTextBox != null)
            {
                // 1. 设置程序化标志，防止 TextChanged 事件中的逻辑干扰
                _isProgrammaticTextChange = true;

                // 2. 同步阶段：赋值
                _translationTextBox.IsEnabled = true;
                _translationTextBox.Watermark = "请输入文本";
                _translationTextBox.Text = targetChildItem.Text;

                _isProgrammaticTextChange = false;

                // 3. 异步渲染后置阶段：操作焦点与光标
                if (Edit.IsEditMode && !_isUpdatingUI)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // 确保控件仍处于可用状态且确实需要焦点
                        if (_translationTextBox.IsEnabled)
                        {
                            _translationTextBox.Focus();
                            // 强制将光标移动到文本末尾
                            _translationTextBox.CaretIndex = _translationTextBox.Text?.Length ?? 0;
                        }
                    }, Avalonia.Threading.DispatcherPriority.Input); // 使用 Input 优先级，确保在 Layout/Render 之后执行
                }
            }
        }
        else if (selectedItem is ImageTreeItem)
        {
            // 如果选中的是图片本身，禁用编辑框
            HighlightLabel(-1);

            if (_translationTextBox != null)
            {
                _translationTextBox.Text = string.Empty;
                _translationTextBox.IsEnabled = false;
                _translationTextBox.Watermark = "选中文本节点以编辑";
            }
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
        
        // 将鼠标侧键转换为 KeyGesture
        var gesture = MouseButtonToKeyGesture(updateKind);
        if (gesture == null)
            return;
        
        // 检查树视图是否有选中项，如果有则处理快捷键
        var selectedItem = Navigation.SelectedItem;
        if (selectedItem != null)
        {
            // 检查是否匹配复制快捷键
            if (_shortcutSettings.CopyText != null && 
                gesture.Equals(_shortcutSettings.CopyText))
            {
                if (selectedItem is TranslationTreeItem translationItem)
                {
                    CopyToClipboard(translationItem.Text);
                    StatusBar.UpdateStatus($"已复制: {translationItem.Text}");
                }
                e.Handled = true;
                return;
            }
            
            // 检查是否匹配上导航快捷键
            bool isNavigateUp = false;
            bool isNavigateDown = false;
            
            if (_shortcutSettings.NavigateUp != null && 
                gesture.Equals(_shortcutSettings.NavigateUp))
            {
                isNavigateUp = true;
            }
            else if (_shortcutSettings.NavigateUpSecondary != null && 
                     gesture.Equals(_shortcutSettings.NavigateUpSecondary))
            {
                isNavigateUp = true;
            }
            else if (_shortcutSettings.NavigateDown != null && 
                     gesture.Equals(_shortcutSettings.NavigateDown))
            {
                isNavigateDown = true;
            }
            else if (_shortcutSettings.NavigateDownSecondary != null && 
                     gesture.Equals(_shortcutSettings.NavigateDownSecondary))
            {
                isNavigateDown = true;
            }
            
            // 执行导航
            if (isNavigateUp || isNavigateDown)
            {
                var visibleItems = Navigation.GetVisibleItems();
                
                int currentIndex = visibleItems.IndexOf(selectedItem);
                if (currentIndex >= 0)
                {
                    if (isNavigateUp && currentIndex > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 鼠标侧键导航: 设置 Navigation.SelectedItem = {visibleItems[currentIndex - 1]}");
                        Navigation.SelectedItem = visibleItems[currentIndex - 1];
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 鼠标侧键导航: 设置后 Nav.SelectedItem={Navigation.SelectedItem}, TreeView.SelectedItem={ImageTreeView.SelectedItem}");
                    }
                    else if (isNavigateDown && currentIndex < visibleItems.Count - 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 鼠标侧键导航: 设置 Navigation.SelectedItem = {visibleItems[currentIndex + 1]}");
                        Navigation.SelectedItem = visibleItems[currentIndex + 1];
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 鼠标侧键导航: 设置后 Nav.SelectedItem={Navigation.SelectedItem}, TreeView.SelectedItem={ImageTreeView.SelectedItem}");
                    }
                }
                e.Handled = true;
            }
        }
    }
    
    /// <summary>
    /// 将鼠标侧键转换为 KeyGesture
    /// </summary>
    private static KeyGesture? MouseButtonToKeyGesture(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            PointerUpdateKind.XButton1Pressed => new KeyGesture(Key.F13),  // 鼠标侧键1
            PointerUpdateKind.XButton2Pressed => new KeyGesture(Key.F14),  // 鼠标侧键2
            _ => null
        };
    }
    
    /// <summary>
    /// 树视图键盘导航处理
    /// </summary>
    private void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        var selectedItem = Navigation.SelectedItem;
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
                StatusBar.UpdateStatus($"已复制: {translationItem.Text}");
            }
            else if (selectedItem is ImageTreeItem imageItem)
            {
                //CopyToClipboard(imageItem.ImageName);
                //StatusBar.UpdateStatus($"已复制: {imageItem.ImageName}");
            }
            e.Handled = true;
            return;
        }
        
        // 检查是否匹配分组切换快捷键
        bool isSwitchToGroup0 = false;
        bool isSwitchToGroup1 = false;
        
        // 检查切换到框内
        if (_shortcutSettings.ToggleGroup0 != null && 
            currentGesture.Equals(_shortcutSettings.ToggleGroup0))
        {
            isSwitchToGroup0 = true;
        }
        // 检查切换到框外
        else if (_shortcutSettings.ToggleGroup1 != null && 
                 currentGesture.Equals(_shortcutSettings.ToggleGroup1))
        {
            isSwitchToGroup1 = true;
        }
        
        // 如果匹配分组切换快捷键，执行分组切换
        if (isSwitchToGroup0 || isSwitchToGroup1)
        {
            if (isSwitchToGroup0)
            {
                SwitchToGroup(0);
            }
            else if (isSwitchToGroup1)
            {
                SwitchToGroup(1);
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
            foreach (var root in Navigation.TreeItems)
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
            var visibleItems = Navigation.GetVisibleItems();
            
            int currentIndex = visibleItems.IndexOf(selectedItem);
            if (currentIndex >= 0)
            {
                if (isNavigateUp && currentIndex > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTreeViewKeyDown: 设置 Navigation.SelectedItem = {visibleItems[currentIndex - 1]}");
                    Navigation.SelectedItem = visibleItems[currentIndex - 1];
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTreeViewKeyDown: 设置后 Navigation.SelectedItem = {Navigation.SelectedItem}, ImageTreeView.SelectedItem = {ImageTreeView.SelectedItem}");
                    e.Handled = true;
                }
                else if (isNavigateDown && currentIndex < visibleItems.Count - 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTreeViewKeyDown: 设置 Navigation.SelectedItem = {visibleItems[currentIndex + 1]}");
                    Navigation.SelectedItem = visibleItems[currentIndex + 1];
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] OnTreeViewKeyDown: 设置后 Navigation.SelectedItem = {Navigation.SelectedItem}, ImageTreeView.SelectedItem = {ImageTreeView.SelectedItem}");
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
                var newSelectedItem = Navigation.SelectedItem;
                if (newSelectedItem == null) return;

                ImageTreeItem? newRootItem = null;
                if (newSelectedItem is ImageTreeItem)
                {
                    newRootItem = newSelectedItem as ImageTreeItem;
                }
                else if (newSelectedItem is TranslationTreeItem newChildItem)
                {
                    foreach (var root in Navigation.TreeItems)
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
                    Navigation.LastFocusedRootItem = newRootItem;
                }
                
                // 确保新选中的项获得焦点，触发视图滚动
                var container = ImageTreeView.ContainerFromItem(newSelectedItem) as Control;
                container?.Focus();
            }, DispatcherPriority.Background);
        }
    }
    
    /// <summary>
    /// 将视野中心对准指定编号的标注（委托给 CanvasViewModel）
    /// </summary>
    private void CenterOnLabel(int labelIndex)
    {
        if (_currentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(_currentImagePath))
            return;
        
        // 获取当前图片的标注数据
        string imageName = Path.GetFileName(_currentImagePath);
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
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
        
        // 委托给 CanvasVM（传入归一化坐标）
        CanvasVM.CenterOnLabel(targetLabel.X, targetLabel.Y);
    }

    // ==================== 树视图拖拽排序 ====================

    /// <summary>
    /// 查找子节点对应的父节点 (ImageTreeItem)
    /// </summary>
    private ImageTreeItem? GetParentImageItem(TranslationTreeItem child)
    {
        return Navigation.GetParentImageItem(child);
    }

    // ==================== TreeViewItem 拖拽事件处理（直接在 DataTemplate 中的元素上绑定）====================

    private void OnTreeViewItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 记录按下的位置和时间
        var point = e.GetCurrentPoint(sender as Control);
        if (point.Properties.IsLeftButtonPressed && sender is Control control)
        {
            var dataContext = control.DataContext;
            System.Diagnostics.Debug.WriteLine($"[TreeView Drag] PointerPressed: sender={sender?.GetType().Name}, DataContext={dataContext?.GetType().Name}");

            // 仅允许拖拽 TranslationTreeItem (子节点)
            if (dataContext is TranslationTreeItem treeItem)
            {
                _treeDragStartPoint = point.Position;
                _isTreeItemDragging = true;
                _draggedTreeItem = treeItem;
                System.Diagnostics.Debug.WriteLine($"[TreeView Drag] Started dragging item: Index={treeItem.Index}, Text={treeItem.Text}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[TreeView Drag] Not a TranslationTreeItem, ignoring");
            }
        }
    }

    private PointerEventArgs? _savedPointerEventArgs;

    private async void OnTreeViewItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isTreeItemDragging || _draggedTreeItem == null) return;

        // 保存 PointerEventArgs 供 DoDragDrop 使用
        _savedPointerEventArgs = e;

        var point = e.GetCurrentPoint(sender as Control);
        var diff = point.Position - _treeDragStartPoint;

        // 判断是否超过了防抖阈值 (例如 3 像素)
        if (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)
        {
            _isTreeItemDragging = false; // 结束指针阶段，进入 DragDrop 阶段

            System.Diagnostics.Debug.WriteLine($"[TreeView Drag] Starting drag for item: {_draggedTreeItem.Index}");

            // 拖拽前先提交当前文本编辑，防止状态丢失
            CommitCurrentEdit();

            // 使用 DataObject 设置拖拽数据
            var dragData = new DataObject();
            dragData.Set("DraggedTranslationItem", _draggedTreeItem);

            // 启动 Avalonia 拖拽
            if (_savedPointerEventArgs != null)
            {
                await DragDrop.DoDragDrop(_savedPointerEventArgs, dragData, DragDropEffects.Move);
            }
            _draggedTreeItem = null;
            _savedPointerEventArgs = null;
        }
    }

    private void OnTreeViewItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isTreeItemDragging = false;
        _draggedTreeItem = null;
    }

    // ==================== TreeView 全局拖放事件处理 ====================

    private void OnTreeViewDragOver(object? sender, DragEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[TreeView Drag] DragOver triggered");

        // 验证拖拽数据是否为标注子项
        if (!e.Data.Contains("DraggedTranslationItem"))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var sourceItem = e.Data.Get("DraggedTranslationItem") as TranslationTreeItem;
        var targetControl = e.Source as Control;
        var targetItem = targetControl?.DataContext;

        if (sourceItem == null || targetItem == null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        // 仅允许放在 TranslationTreeItem 上
        if (targetItem is TranslationTreeItem targetTranslationItem)
        {
            // 判断是否属于同一个图片 (不允许跨图片拖拽)
            var sourceParent = GetParentImageItem(sourceItem);
            var targetParent = GetParentImageItem(targetTranslationItem);

            if (sourceParent != null && sourceParent == targetParent)
            {
                e.DragEffects = DragDropEffects.Move;
                return;
            }
        }

        e.DragEffects = DragDropEffects.None;
    }

    private void OnTreeViewDrop(object? sender, DragEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[TreeView Drag] Drop triggered");

        if (!e.Data.Contains("DraggedTranslationItem")) return;

        var sourceItem = e.Data.Get("DraggedTranslationItem") as TranslationTreeItem;
        var targetControl = e.Source as Control;
        var targetItem = targetControl?.DataContext as TranslationTreeItem;

        if (sourceItem == null || targetItem == null || sourceItem == targetItem) return;

        var parentImageItem = GetParentImageItem(sourceItem);
        if (parentImageItem == null || parentImageItem != GetParentImageItem(targetItem)) return;

        System.Diagnostics.Debug.WriteLine($"[TreeView Drag] Drop: {sourceItem.Index} -> {targetItem.Index}");

        // 获取底层数据并执行命令
        if (Document.TranslationData != null)
        {
            string imageName = parentImageItem.ImageName;
            if (Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
            {
                // 找到对应的底层模型
                var sourceModel = labels.FirstOrDefault(l => l.TextIndex == sourceItem.Index);
                var targetModel = labels.FirstOrDefault(l => l.TextIndex == targetItem.Index);

                if (sourceModel != null && targetModel != null)
                {
                    int targetIndex = labels.IndexOf(targetModel);

                    // 执行拖拽重排命令
                    CanvasVM.ReorderLabels(labels, sourceModel, targetIndex, targetIndex + 1);
                }
            }
        }
    }
}