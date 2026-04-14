using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LabelAva.Models;
using LabelAva.Services;

namespace LabelAva.Views;

public partial class PreferencesWindow : Window
{
    // 快捷键设置
    private ShortcutSettings _settings = new();

    // 当前正在捕获按键的按钮
    private Button? _capturingButton;
    
    private SettingsChangeKind _changeKind = SettingsChangeKind.None;

    // 防止 UpdateUI() 程序化赋值时触发变更处理器
    private bool _isUpdatingUI = false;

    public static event EventHandler<(ShortcutSettings settings, SettingsChangeKind changes, bool isPreview)>? SettingsChanged;
    
    public PreferencesWindow()
    {
        InitializeComponent();
        
        // 加载设置
        LoadSettings();
        
        // 更新界面显示
        UpdateUI();
        
        // 窗口关闭时统一保存并通知变更
        this.Closed += OnPreferencesWindowClosed;
    }
    
    /// <summary>
    /// 加载快捷键设置
    /// </summary>
    private void LoadSettings()
    {
        _settings = ShortcutSettingsService.Load();
    }
    
    /// <summary>
    /// 更新界面显示
    /// </summary>
    private void UpdateUI()
    {
        _isUpdatingUI = true;
        try
        {
            // 更新导航上
            UpdateButtonDisplay(NavigateUpButton, NavigateUpText, _settings.NavigateUp);

            // 更新导航上（次要）
            UpdateButtonDisplay(NavigateUpSecondaryButton, NavigateUpSecondaryText, _settings.NavigateUpSecondary);

            // 更新导航下
            UpdateButtonDisplay(NavigateDownButton, NavigateDownText, _settings.NavigateDown);

            // 更新导航下（次要）
            UpdateButtonDisplay(NavigateDownSecondaryButton, NavigateDownSecondaryText, _settings.NavigateDownSecondary);

            // 更新复制文本
            UpdateButtonDisplay(CopyTextButton, CopyTextText, _settings.CopyText);
            UpdateButtonDisplay(DeleteLabelButton, DeleteLabelText, _settings.DeleteLabel);
            UpdateButtonDisplay(OpenFileButton, OpenFileText, _settings.OpenFile);
            UpdateButtonDisplay(SaveFileButton, SaveFileText, _settings.SaveFile);

            // 更新分组切换
            UpdateButtonDisplay(ToggleGroup0Button, ToggleGroup0Text, _settings.ToggleGroup0);
            UpdateButtonDisplay(ToggleGroup1Button, ToggleGroup1Text, _settings.ToggleGroup1);

            // 更新颜色设置
            UpdateColorUI();

            // 更新自动聚焦设置
            if (AutoFocusCheckBox != null)
            {
                AutoFocusCheckBox.IsChecked = _settings.AutoFocusTextBox;
            }
            
            // 更新标号大小设置
            if (LabelSizeUpDown != null)
            {
                LabelSizeUpDown.Value = _settings.LabelSize;
            }
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    /// <summary>
    /// 更新颜色设置界面
    /// </summary>
    private void UpdateColorUI()
    {
        var colors = _settings.Colors;

        // 框内颜色 (GroupIndex 1)
        if (colors.GroupColors.TryGetValue(1, out var group0Color))
        {
            Group0ColorTextBox.Text = group0Color;
            UpdateColorPreview(Group0ColorPreview, group0Color);
        }

        // 框外颜色 (GroupIndex 2)
        if (colors.GroupColors.TryGetValue(2, out var group1Color))
        {
            Group1ColorTextBox.Text = group1Color;
            UpdateColorPreview(Group1ColorPreview, group1Color);
        }

        // 选中高亮颜色
        SelectedColorTextBox.Text = colors.SelectedColor;
        UpdateColorPreview(SelectedColorPreview, colors.SelectedColor);
    }

    /// <summary>
    /// 更新颜色预览边框的背景色
    /// </summary>
    private void UpdateColorPreview(Border border, string? colorHex)
    {
        if (border == null) return;

        try
        {
            if (!string.IsNullOrEmpty(colorHex) && colorHex.StartsWith("#"))
            {
                var color = Color.Parse(colorHex);
                border.Background = new SolidColorBrush(color);
            }
            else
            {
                // 默认颜色
                border.Background = new SolidColorBrush(Colors.LightGray);
            }
        }
        catch
        {
            border.Background = new SolidColorBrush(Colors.LightGray);
        }
    }

    /// <summary>
    /// 框内颜色变更事件
    /// </summary>
    private void OnGroup0ColorChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI) return;
        var textBox = sender as TextBox;
        if (textBox == null) return;

        var colorHex = textBox.Text;
        if (IsValidColorHex(colorHex))
        {
            _settings.Colors.GroupColors[1] = colorHex!;
            UpdateColorPreview(Group0ColorPreview, colorHex);
            _changeKind |= SettingsChangeKind.Colors;
        }
    }

    /// <summary>
    /// 框外颜色变更事件
    /// </summary>
    private void OnGroup1ColorChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI) return;
        var textBox = sender as TextBox;
        if (textBox == null) return;

        var colorHex = textBox.Text;
        if (IsValidColorHex(colorHex))
        {
            _settings.Colors.GroupColors[2] = colorHex!;
            UpdateColorPreview(Group1ColorPreview, colorHex);
            _changeKind |= SettingsChangeKind.Colors;
        }
    }

    /// <summary>
    /// 选中高亮颜色变更事件
    /// </summary>
    private void OnSelectedColorChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI) return;
        var textBox = sender as TextBox;
        if (textBox == null) return;

        var colorHex = textBox.Text;
        if (IsValidColorHex(colorHex))
        {
            _settings.Colors.SelectedColor = colorHex!;
            UpdateColorPreview(SelectedColorPreview, colorHex);
            _changeKind |= SettingsChangeKind.Colors;
        }
    }

    /// <summary>
    /// 验证颜色代码是否有效
    /// </summary>
    private bool IsValidColorHex(string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return false;

        try
        {
            Color.Parse(colorHex);
            return colorHex.StartsWith("#") && (colorHex.Length == 7 || colorHex.Length == 9);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 重置颜色为默认
    /// </summary>
    private void OnResetColorDefaults(object? sender, RoutedEventArgs e)
    {
        _settings.Colors = ColorSettings.CreateDefaults();
        UpdateColorUI();
        _changeKind |= SettingsChangeKind.Colors;
    }
    
    /// <summary>
    /// 更新按钮的显示文字和样式
    /// </summary>
    private void UpdateButtonDisplay(Button button, TextBlock? textBlock, KeyGesture? gesture)
    {
        if (textBlock == null) return;
        
        string text = ShortcutSettings.KeyGestureToString(gesture);
        textBlock.Text = text;
        
        // 设置颜色：如果未设置显示灰色，否则显示黑色
        if (text == "未设置")
        {
            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#999999"));
        }
        else
        {
            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
        }
    }
    
    /// <summary>
    /// 处理上导航主要快捷键按钮点击
    /// </summary>
    private void OnNavigateUpClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(NavigateUpButton, (gesture) =>
        {
            _settings.NavigateUp = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }
    
    /// <summary>
    /// 处理上导航次要快捷键按钮点击
    /// </summary>
    private void OnNavigateUpSecondaryClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(NavigateUpSecondaryButton, (gesture) =>
        {
            _settings.NavigateUpSecondary = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }
    
    /// <summary>
    /// 处理下导航主要快捷键按钮点击
    /// </summary>
    private void OnNavigateDownClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(NavigateDownButton, (gesture) =>
        {
            _settings.NavigateDown = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }
    
    /// <summary>
    /// 处理下导航次要快捷键按钮点击
    /// </summary>
    private void OnNavigateDownSecondaryClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(NavigateDownSecondaryButton, (gesture) =>
        {
            _settings.NavigateDownSecondary = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }
    
    /// <summary>
    /// 处理复制文本快捷键按钮点击
    /// </summary>
    private void OnCopyTextClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(CopyTextButton, (gesture) =>
        {
            _settings.CopyText = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }

    private void OnDeleteLabelClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(DeleteLabelButton, (gesture) =>
        {
            _settings.DeleteLabel = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(OpenFileButton, (gesture) =>
        {
            _settings.OpenFile = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }

    private void OnSaveFileClick(object? sender, RoutedEventArgs e)
    {
        StartCapture(SaveFileButton, (gesture) =>
        {
            _settings.SaveFile = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }

    /// <summary>
    /// 处理自动聚焦开关变更
    /// </summary>
    private void OnAutoFocusChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI) return;
        if (AutoFocusCheckBox == null) return;

        _settings.AutoFocusTextBox = AutoFocusCheckBox.IsChecked == true;
        _changeKind |= SettingsChangeKind.AutoFocus;
    }
    
    /// <summary>
    /// 处理标号大小变更（实时预览，不保存到文件；关闭窗口时统一保存）
    /// </summary>
    private void OnLabelSizeChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingUI) return;
        if (LabelSizeUpDown == null) return;

        _settings.LabelSize = (int)(LabelSizeUpDown.Value ?? 64);
        _changeKind |= SettingsChangeKind.LabelSize;
        NotifySettingsChanged(SettingsChangeKind.LabelSize, isPreview: true);
    }
    
    /// <summary>
    /// 处理切换框内分组快捷键按钮点击
    /// </summary>
    private void OnToggleGroup0Click(object? sender, RoutedEventArgs e)
    {
        StartCapture(ToggleGroup0Button, (gesture) =>
        {
            _settings.ToggleGroup0 = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }
    
    /// <summary>
    /// 处理切换框外分组快捷键按钮点击
    /// </summary>
    private void OnToggleGroup1Click(object? sender, RoutedEventArgs e)
    {
        StartCapture(ToggleGroup1Button, (gesture) =>
        {
            _settings.ToggleGroup1 = gesture;
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }
    
    /// <summary>
    /// 开始捕获按键
    /// </summary>
    private void StartCapture(Button button, Action<KeyGesture?> onCaptured)
    {
        // 如果已经在捕获状态，先取消
        if (_capturingButton != null)
        {
            CancelCapture();
        }
        
        _capturingButton = button;
        
        // 更新按钮显示
        var textBlock = button.Classes.Contains("capturing") ? null : 
            button.Content as TextBlock;
        if (textBlock != null)
        {
            textBlock.Text = "请按键或鼠标...";
            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0078D4"));
        }
        
        button.Classes.Add("capturing");
        
        // 订阅键盘和鼠标事件
        this.KeyDown += OnKeyDown;
        this.PointerPressed += OnPointerPressed;
        this.PointerWheelChanged += OnPointerWheelChanged;
        this.Focus();
        
        // 保存回调
        _captureCallback = onCaptured;
    }
    
    private Action<KeyGesture?>? _captureCallback;
    
    /// <summary>
    /// 处理键盘按键事件
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        
        // 如果按下 Escape，取消捕获
        if (e.Key == Key.Escape)
        {
            CancelCapture();
            return;
        }
        
        // 如果没有有效按键，忽略
        if (e.Key == Key.None)
            return;
        
        // 如果按下的仅仅是修饰键本身（例如刚按下 Ctrl，尚未按下主按键 C），则不结束捕获，直接返回继续等待后续击键
        if (ShortcutSettings.IsModifierKey(e.Key) || e.Key == Key.System)
        {
            return;
        }
        
        // 创建快捷键
        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        
        // 完成捕获
        CompleteCapture(gesture);
    }
    
    /// <summary>
    /// 处理鼠标按键事件
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        var updateKind = properties.PointerUpdateKind;
        
        // 检查是否为黑名单中的鼠标按钮（左键、右键、滚轮中键）
        if (ShortcutSettings.IsBlacklistedMouseButton(updateKind))
        {
            // 显示不支持的提示，但不结束捕获
            ShowBlacklistedMessage();
            return;
        }
        
        // 尝试从鼠标按钮创建手势
        var gesture = CreateMouseGesture(updateKind);
        if (gesture != null)
        {
            e.Handled = true;
            CompleteCapture(gesture);
        }
    }
    
    /// <summary>
    /// 处理鼠标滚轮事件
    /// </summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        
        // 滚轮不支持作为快捷键
        ShowBlacklistedMessage();
    }
    
    /// <summary>
    /// 显示黑名单提示
    /// </summary>
    private void ShowBlacklistedMessage()
    {
        // 可以在此处显示提示，但为了不打断捕获流程，这里仅输出日志
        System.Console.WriteLine("左键、右键、滚轮中键不支持作为快捷键");
    }
    
    /// <summary>
    /// 从鼠标按键创建手势（用于支持有侧键的鼠标）
    /// 只支持 XButton1、XButton2 侧键
    /// </summary>
    private static KeyGesture? CreateMouseGesture(PointerUpdateKind updateKind)
    {
        // 只处理按下事件，不处理释放事件
        var isPressed = updateKind switch
        {
            PointerUpdateKind.XButton1Pressed => true,
            PointerUpdateKind.XButton2Pressed => true,
            _ => false
        };
        
        if (!isPressed)
            return null;
        
        // 根据按键类型创建手势
        // 注意：Avalonia 的 Key 枚举没有 XButton1/XButton2
        // 使用 F13/F14 作为占位符，因为这些键很少使用
        var key = updateKind switch
        {
            PointerUpdateKind.XButton1Pressed => Key.F13,
            PointerUpdateKind.XButton2Pressed => Key.F14,
            _ => Key.None
        };
        
        if (key == Key.None)
            return null;
        
        return new KeyGesture(key);
    }
    
    /// <summary>
    /// 完成按键捕获
    /// </summary>
    private void CompleteCapture(KeyGesture gesture)
    {
        if (_capturingButton != null && _captureCallback != null)
        {
            // 调用回调
            _captureCallback(gesture);
            
            // 移除捕获状态
            _capturingButton.Classes.Remove("capturing");
            _capturingButton = null;
            _captureCallback = null;
            
            // 取消订阅所有事件
            this.KeyDown -= OnKeyDown;
            this.PointerPressed -= OnPointerPressed;
            this.PointerWheelChanged -= OnPointerWheelChanged;
        }
    }
    
    /// <summary>
    /// 取消按键捕获
    /// </summary>
    private void CancelCapture()
    {
        if (_capturingButton != null)
        {
            _capturingButton.Classes.Remove("capturing");
            _capturingButton = null;
            _captureCallback = null;
            
            // 取消订阅所有事件
            this.KeyDown -= OnKeyDown;
            this.PointerPressed -= OnPointerPressed;
            this.PointerWheelChanged -= OnPointerWheelChanged;
            
            // 更新界面
            UpdateUI();
        }
    }
    
    /// <summary>
    /// 恢复默认设置
    /// </summary>
    private void OnResetDefaults(object? sender, RoutedEventArgs e)
    {
        _settings = ShortcutSettings.CreateDefaults();
        UpdateUI();
        _changeKind |= SettingsChangeKind.All;
    }
    
    /// <summary>
    /// 保存并通知设置已更改
    /// </summary>
    private void SaveAndNotify()
    {
        ShortcutSettingsService.Save(_settings);
        NotifySettingsChanged(_changeKind, isPreview: false);
    }
    
    private void NotifySettingsChanged(SettingsChangeKind changes, bool isPreview)
    {
        SettingsChanged?.Invoke(this, (_settings, changes, isPreview));
    }
    
    private void OnPreferencesWindowClosed(object? sender, EventArgs e)
    {
        this.Closed -= OnPreferencesWindowClosed;
        
        if (_changeKind != SettingsChangeKind.None)
        {
            ShortcutSettingsService.Save(_settings);
            SettingsChanged?.Invoke(this, (_settings, _changeKind, isPreview: false));
            _changeKind = SettingsChangeKind.None;
        }
    }
    
    /// <summary>
    /// 获取当前快捷键设置（静态方法，供外部调用）
    /// </summary>
    public static ShortcutSettings GetCurrentSettings()
    {
        return ShortcutSettingsService.Load();
    }
}
