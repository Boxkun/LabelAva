using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LabelAva.Models;
using LabelAva.Services;

namespace LabelAva.Views;

public partial class PreferencesWindow : Window
{
    // 快捷键设置
    private ShortcutSettings _settings = new();
    
    // 当前正在捕获按键的按钮
    private Button? _capturingButton;
    
    // 静态事件用于通知 MainWindow 设置已更改
    public static event EventHandler<ShortcutSettings>? SettingsChanged;
    
    public PreferencesWindow()
    {
        InitializeComponent();
        
        // 加载设置
        LoadSettings();
        
        // 更新界面显示
        UpdateUI();
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
            SaveAndNotify();
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
            SaveAndNotify();
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
            SaveAndNotify();
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
            SaveAndNotify();
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
            SaveAndNotify();
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
            textBlock.Text = "请按键...";
            textBlock.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0078D4"));
        }
        
        button.Classes.Add("capturing");
        
        // 订阅键盘事件
        this.KeyDown += OnKeyDown;
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
            
            // 取消订阅
            this.KeyDown -= OnKeyDown;
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
            
            // 取消订阅
            this.KeyDown -= OnKeyDown;
            
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
        
        // 保存到文件
        ShortcutSettingsService.Save(_settings);
        
        NotifySettingsChanged();
    }
    
    /// <summary>
    /// 保存并通知设置已更改
    /// </summary>
    private void SaveAndNotify()
    {
        // 保存到文件
        ShortcutSettingsService.Save(_settings);
        
        // 通知更改
        NotifySettingsChanged();
    }
    
    /// <summary>
    /// 通知设置已更改
    /// </summary>
    private void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke(this, _settings);
    }
    
    /// <summary>
    /// 获取当前快捷键设置（静态方法，供外部调用）
    /// </summary>
    public static ShortcutSettings GetCurrentSettings()
    {
        return ShortcutSettingsService.Load();
    }
}
