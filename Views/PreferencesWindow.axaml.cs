using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LabelAva.Services;
using LabelAva.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace LabelAva.Views;

public partial class PreferencesWindow : Window
{
    private readonly AppSettingsProvider _provider;
    private AppSettings _settings;
    private ObservableCollection<QuickInputSlot> _defaultQuickInputs = null!;
    private ObservableCollection<QuickInputSlot> _dligQuickInputs = null!;
    private Button? _capturingButton;
    private SettingsChangeKind _changeKind = SettingsChangeKind.None;
    private bool _isUpdatingUI = false;
    private Action<KeyGesture?>? _captureCallback;

    public PreferencesWindow() : this(new AppSettingsProvider()) { }

    public PreferencesWindow(AppSettingsProvider provider)
    {
        InitializeComponent();
        _provider = provider;
        _settings = CloneSettings(_provider.Current);
        UpdateUI();
        this.Closed += OnPreferencesWindowClosed;
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            Shortcuts = source.Shortcuts.Clone(),
            Colors = source.Colors.Clone(),
            LabelSize = source.LabelSize,
            AutoFocusTextBox = source.AutoFocusTextBox,
            ActiveDligConfig = source.ActiveDligConfig,
            DefaultQuickInputs = source.DefaultQuickInputs.Select(s => new QuickInputSlot { Label = s.Label, Character = s.Character }).ToList(),
            MouseConfig = source.MouseConfig,
        };
    }

    private void UpdateUI()
    {
        _isUpdatingUI = true;
        try
        {
            var s = _settings.Shortcuts;
            UpdateButtonDisplay(NavigateUpButton, NavigateUpText, s.NavigateUp);
            UpdateButtonDisplay(NavigateUpSecondaryButton, NavigateUpSecondaryText, s.NavigateUpSecondary);
            UpdateButtonDisplay(NavigateDownButton, NavigateDownText, s.NavigateDown);
            UpdateButtonDisplay(NavigateDownSecondaryButton, NavigateDownSecondaryText, s.NavigateDownSecondary);
            UpdateButtonDisplay(CopyTextButton, CopyTextText, s.CopyText);
            UpdateButtonDisplay(DeleteLabelButton, DeleteLabelText, s.DeleteLabel);
            UpdateButtonDisplay(OpenFileButton, OpenFileText, s.OpenFile);
            UpdateButtonDisplay(SaveFileButton, SaveFileText, s.SaveFile);
            UpdateButtonDisplay(SaveAsFileButton, SaveAsFileText, s.SaveAsFile);
            UpdateButtonDisplay(ToggleGroup0Button, ToggleGroup0Text, s.ToggleGroup0);
            UpdateButtonDisplay(ToggleGroup1Button, ToggleGroup1Text, s.ToggleGroup1);
            UpdateButtonDisplay(PageUpButton, PageUpText, s.PageUp);
            UpdateButtonDisplay(PageDownButton, PageDownText, s.PageDown);

            UpdateColorUI();

            UpdateDligUI();

            // 默认快捷输入
            _defaultQuickInputs = new ObservableCollection<QuickInputSlot>(
                _settings.DefaultQuickInputs.Select(s => new QuickInputSlot { Label = s.Label, Character = s.Character }));
            DefaultQuickInputItemsControl.ItemsSource = _defaultQuickInputs;

            if (AutoFocusCheckBox != null)
                AutoFocusCheckBox.IsChecked = _settings.AutoFocusTextBox;

            if (LabelSizeUpDown != null)
                LabelSizeUpDown.Value = _settings.LabelSize;

            InitMouseConfigUI();
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private void UpdateColorUI()
    {
        var colors = _settings.Colors;

        if (colors.GroupColors.TryGetValue(1, out var group0Color))
        {
            Group0ColorTextBox.Text = group0Color;
            UpdateColorPreview(Group0ColorPreview, group0Color);
        }

        if (colors.GroupColors.TryGetValue(2, out var group1Color))
        {
            Group1ColorTextBox.Text = group1Color;
            UpdateColorPreview(Group1ColorPreview, group1Color);
        }

        SelectedColorTextBox.Text = colors.SelectedColor;
        UpdateColorPreview(SelectedColorPreview, colors.SelectedColor);
    }

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
                border.Background = Services.ThemeHelper.GetBrush("SystemControlForegroundChromeDisabledBrush")
                    ?? new SolidColorBrush(Colors.LightGray);
            }
        }
        catch
        {
            border.Background = Services.ThemeHelper.GetBrush("SystemControlForegroundChromeDisabledBrush")
                ?? new SolidColorBrush(Colors.LightGray);
        }
    }

    private void UpdateButtonDisplay(Button button, TextBlock? textBlock, KeyGesture? gesture)
    {
        if (textBlock == null) return;

        string text = ShortcutBindings.KeyGestureToString(gesture);
        textBlock.Text = text;

        if (text == "未设置")
        {
            button.Classes.Add("unset");
        }
        else
        {
            button.Classes.Remove("unset");
        }
    }

    private void CaptureShortcut(Button button, Action<KeyGesture?> assignAction)
    {
        StartCapture(button, gesture =>
        {
            assignAction(gesture);
            UpdateUI();
            _changeKind |= SettingsChangeKind.Shortcuts;
        });
    }

    private void OnNavigateUpClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(NavigateUpButton, g => _settings.Shortcuts.NavigateUp = g);
    private void OnNavigateUpSecondaryClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(NavigateUpSecondaryButton, g => _settings.Shortcuts.NavigateUpSecondary = g);
    private void OnNavigateDownClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(NavigateDownButton, g => _settings.Shortcuts.NavigateDown = g);
    private void OnNavigateDownSecondaryClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(NavigateDownSecondaryButton, g => _settings.Shortcuts.NavigateDownSecondary = g);
    private void OnCopyTextClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(CopyTextButton, g => _settings.Shortcuts.CopyText = g);
    private void OnDeleteLabelClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(DeleteLabelButton, g => _settings.Shortcuts.DeleteLabel = g);
    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(OpenFileButton, g => _settings.Shortcuts.OpenFile = g);
    private void OnSaveFileClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(SaveFileButton, g => _settings.Shortcuts.SaveFile = g);
    private void OnSaveAsFileClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(SaveAsFileButton, g => _settings.Shortcuts.SaveAsFile = g);
    private void OnToggleGroup0Click(object? sender, RoutedEventArgs e)
        => CaptureShortcut(ToggleGroup0Button, g => _settings.Shortcuts.ToggleGroup0 = g);
    private void OnToggleGroup1Click(object? sender, RoutedEventArgs e)
        => CaptureShortcut(ToggleGroup1Button, g => _settings.Shortcuts.ToggleGroup1 = g);
    private void OnPageUpClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(PageUpButton, g => _settings.Shortcuts.PageUp = g);
    private void OnPageDownClick(object? sender, RoutedEventArgs e)
        => CaptureShortcut(PageDownButton, g => _settings.Shortcuts.PageDown = g);

    private void OnAutoFocusChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || AutoFocusCheckBox == null) return;
        _settings.AutoFocusTextBox = AutoFocusCheckBox.IsChecked == true;
        _changeKind |= SettingsChangeKind.AutoFocus;
    }

    private void OnLabelSizeChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingUI || LabelSizeUpDown == null) return;
        _settings.LabelSize = (int)(LabelSizeUpDown.Value ?? 32);
        _changeKind |= SettingsChangeKind.LabelSize;
    }

    private void ApplyColorChange(TextBox? textBox, Border preview, Action<ColorSettings, string> applyAction)
    {
        if (_isUpdatingUI || textBox == null) return;
        var colorHex = textBox.Text;
        if (IsValidColorHex(colorHex))
        {
            applyAction(_settings.Colors, colorHex!);
            UpdateColorPreview(preview, colorHex);
            _changeKind |= SettingsChangeKind.Colors;
        }
    }

    private void OnGroup0ColorChanged(object? sender, TextChangedEventArgs e)
        => ApplyColorChange(sender as TextBox, Group0ColorPreview, (c, hex) => c.GroupColors[1] = hex);
    private void OnGroup1ColorChanged(object? sender, TextChangedEventArgs e)
        => ApplyColorChange(sender as TextBox, Group1ColorPreview, (c, hex) => c.GroupColors[2] = hex);
    private void OnSelectedColorChanged(object? sender, TextChangedEventArgs e)
        => ApplyColorChange(sender as TextBox, SelectedColorPreview, (c, hex) => c.SelectedColor = hex);

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

    private void OnResetColorDefaults(object? sender, RoutedEventArgs e)
    {
        _settings.Colors = ColorSettings.CreateDefaults();
        UpdateColorUI();
        _changeKind |= SettingsChangeKind.Colors;
    }

    private void OnResetDefaults(object? sender, RoutedEventArgs e)
    {
        _settings = AppSettings.CreateDefaults();
        UpdateUI();
        _changeKind |= SettingsChangeKind.All;
    }

    private static readonly string[] MouseActionLabels = { "添加/选中", "平移", "删除", "显示菜单", "无" };

    private void InitMouseConfigUI()
    {
        // 保存当前模式，避免缓存 SelectedIndex 期间触发回写覆盖
        var savedCfg = _settings.MouseConfig;

        // 先填充所有选项再统一设置选中，避免 Clear→Add 期间的 -1 事件风暴
        foreach (var cb in new[] { MouseLeftButtonComboBox, MouseMiddleButtonComboBox, MouseRightButtonComboBox })
        {
            if (cb == null) continue;
            cb.Items.Clear();
            foreach (var label in MouseActionLabels)
                cb.Items.Add(label);
        }

        if (MouseLeftButtonComboBox != null)
            MouseLeftButtonComboBox.SelectedIndex = (int)savedCfg.LeftButton;
        if (MouseMiddleButtonComboBox != null)
            MouseMiddleButtonComboBox.SelectedIndex = (int)savedCfg.MiddleButton;
        if (MouseRightButtonComboBox != null)
            MouseRightButtonComboBox.SelectedIndex = (int)savedCfg.RightButton;

        RefreshConflictHighlights();
    }

    private void OnMouseButtonSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        // 仅从当前引发事件的 ComboBox 读取值，其他两个从 _settings 中取旧值
        var cfg = _settings.MouseConfig;
        if (sender == MouseLeftButtonComboBox)
            cfg.LeftButton = SafeIndex(MouseLeftButtonComboBox);
        if (sender == MouseMiddleButtonComboBox)
            cfg.MiddleButton = SafeIndex(MouseMiddleButtonComboBox);
        if (sender == MouseRightButtonComboBox)
            cfg.RightButton = SafeIndex(MouseRightButtonComboBox);
        _settings.MouseConfig = cfg;

        _changeKind |= SettingsChangeKind.CanvasMouse;
        RefreshConflictHighlights();
    }

    private static CanvasMouseAction SafeIndex(ComboBox? cb)
        => cb?.SelectedIndex is >= 0 and < 5
            ? (CanvasMouseAction)cb.SelectedIndex
            : CanvasMouseAction.None;

    private void RefreshConflictHighlights()
    {
        var cfg = _settings.MouseConfig;
        bool conflict = cfg.HasConflict();

        if (MouseConflictHint != null)
            MouseConflictHint.IsVisible = conflict;

        var actions = new[] { cfg.LeftButton, cfg.MiddleButton, cfg.RightButton };
        var dupes = actions
            .Where(a => a != CanvasMouseAction.None)
            .GroupBy(a => a)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var redBrush = new SolidColorBrush(Color.Parse("#E74856"));
        SetConflictBorder(MouseLeftButtonComboBox,   dupes.Contains(cfg.LeftButton)   ? redBrush : null);
        SetConflictBorder(MouseMiddleButtonComboBox, dupes.Contains(cfg.MiddleButton) ? redBrush : null);
        SetConflictBorder(MouseRightButtonComboBox,  dupes.Contains(cfg.RightButton)  ? redBrush : null);
    }

    private void SetConflictBorder(ComboBox? cb, IBrush? conflictBrush)
    {
        if (cb == null) return;
        if (conflictBrush != null)
        {
            cb.BorderBrush = conflictBrush;
            cb.BorderThickness = new Thickness(2);
        }
        else
        {
            // 清除本地值，回退到 Controls.axaml 中的 ComboBox 样式
            cb.ClearValue(ComboBox.BorderBrushProperty);
            cb.ClearValue(ComboBox.BorderThicknessProperty);
        }
    }

    // ========================
    // 字体/连字配置
    // ========================

    private string? _currentDligConfigName;
    private DligFontConfig? _currentDligConfig;

    private void UpdateDligUI()
    {
        if (DligConfigComboBox == null) return;

        _isUpdatingUI = true;
        try
        {
            var configs = DligConfigService.ListConfigNames();

            DligConfigComboBox.Items.Clear();
            DligConfigComboBox.Items.Add("(无)");
            foreach (var name in configs)
                DligConfigComboBox.Items.Add(name);

            var activeConfig = _settings.ActiveDligConfig;
            if (!string.IsNullOrWhiteSpace(activeConfig) && configs.Contains(activeConfig))
            {
                DligConfigComboBox.SelectedIndex = configs.IndexOf(activeConfig) + 1;
            }
            else
            {
                DligConfigComboBox.SelectedIndex = 0;
            }

            LoadCurrentDligConfigFields();
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private void LoadCurrentDligConfigFields()
    {
        if (DligConfigComboBox.SelectedIndex <= 0)
        {
            _currentDligConfigName = null;
            _currentDligConfig = null;
        }
        else
        {
            _currentDligConfigName = DligConfigComboBox.SelectedItem?.ToString();
            _currentDligConfig = DligConfigService.LoadConfig(_currentDligConfigName!) ?? new DligFontConfig();
        }

        RefreshDligFields();
    }

    private void RefreshDligFields()
    {
        var isEditable = _currentDligConfigName != null;

        if (DligFontFamilyTextBox != null)
        {
            DligFontFamilyTextBox.IsEnabled = isEditable;
            DligFontFamilyTextBox.Text = _currentDligConfig?.FontFamily ?? "";
        }

        if (DligFontFeaturesTextBox != null)
        {
            DligFontFeaturesTextBox.IsEnabled = isEditable;
            DligFontFeaturesTextBox.Text = _currentDligConfig?.FontFeatures ?? "";
        }

        DeleteDligConfigButton.IsEnabled = isEditable;

        if (QuickInputSlotsItemsControl != null)
        {
            _dligQuickInputs = new ObservableCollection<QuickInputSlot>(
                _currentDligConfig?.QuickInputs?.Select(s => new QuickInputSlot { Label = s.Label, Character = s.Character }) ?? Array.Empty<QuickInputSlot>());
            QuickInputSlotsItemsControl.ItemsSource = _dligQuickInputs;
        }
    }

    private void CommitCurrentDligConfig()
    {
        if (_currentDligConfigName == null || _currentDligConfig == null) return;

        _currentDligConfig.FontFamily = DligFontFamilyTextBox?.Text ?? "";
        _currentDligConfig.FontFeatures = DligFontFeaturesTextBox?.Text ?? "dlig=1";

        // 同步回 ObservableCollection 到 config，过滤空行
        _currentDligConfig.QuickInputs = _dligQuickInputs?
            .Where(s => !string.IsNullOrEmpty(s.Character))
            .Select(s => new QuickInputSlot { Label = s.Label ?? "", Character = s.Character })
            .ToList() ?? new List<QuickInputSlot>();

        DligConfigService.SaveConfig(_currentDligConfigName, _currentDligConfig);
    }

    private void OnDligConfigSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        if (DligConfigComboBox.SelectedIndex > 0)
        {
            CommitCurrentDligConfig();
        }

        LoadCurrentDligConfigFields();

        var newConfigName = DligConfigComboBox.SelectedIndex > 0
            ? DligConfigComboBox.SelectedItem?.ToString()
            : null;

        if (_settings.ActiveDligConfig == newConfigName) return;

        _settings.ActiveDligConfig = newConfigName;
        _changeKind |= SettingsChangeKind.DligConfig;
    }

    private void OnNewDligConfig(object? sender, RoutedEventArgs e)
    {
        CommitCurrentDligConfig();

        var configName = "新配置";
        var configs = DligConfigService.ListConfigNames();
        var baseName = configName;
        var counter = 1;
        while (configs.Contains(configName))
        {
            configName = $"{baseName} {counter}";
            counter++;
        }

        DligConfigService.SaveConfig(configName, new DligFontConfig());
        _changeKind |= SettingsChangeKind.DligConfig;
        UpdateDligUI();

        var newIndex = DligConfigComboBox.Items.IndexOf(configName);
        if (newIndex >= 0)
            DligConfigComboBox.SelectedIndex = newIndex;
    }

    private void OnDeleteDligConfig(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentDligConfigName)) return;

        DligConfigService.DeleteConfig(_currentDligConfigName);

        if (_settings.ActiveDligConfig == _currentDligConfigName)
        {
            _settings.ActiveDligConfig = null;
        }

        _currentDligConfigName = null;
        _currentDligConfig = null;
        _changeKind |= SettingsChangeKind.DligConfig;
        UpdateDligUI();
    }

    private void OnOpenDligConfFolder(object? sender, RoutedEventArgs e)
    {
        DligConfigService.EnsureDirectory();
        var dir = DligConfigService.GetConfigDir();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnAddDefaultQuickInput(object? sender, RoutedEventArgs e)
    {
        _defaultQuickInputs.Add(new QuickInputSlot());
        _changeKind |= SettingsChangeKind.DligConfig;
    }

    private void OnDeleteDefaultQuickInput(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is QuickInputSlot slot)
        {
            _defaultQuickInputs.Remove(slot);
            _changeKind |= SettingsChangeKind.DligConfig;
        }
    }

    private void OnAddDligQuickInput(object? sender, RoutedEventArgs e)
    {
        if (_currentDligConfig == null)
        {
            if (_currentDligConfigName == null)
            {
                OnNewDligConfig(sender, e);
                // RefreshDligFields will be called by the new config flow, which re-creates _dligQuickInputs
                return;
            }
            _currentDligConfig = new DligFontConfig();
        }
        _dligQuickInputs.Add(new QuickInputSlot());
        _changeKind |= SettingsChangeKind.DligConfig;
    }

    private void OnDeleteDligQuickInput(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is QuickInputSlot slot)
        {
            _dligQuickInputs.Remove(slot);
            _changeKind |= SettingsChangeKind.DligConfig;
        }
    }

    // ========================
    // 快捷键捕获
    // ========================

    private void StartCapture(Button button, Action<KeyGesture?> onCaptured)
    {
        if (_capturingButton != null)
            CancelCapture();

        _capturingButton = button;

        var textBlock = button.Classes.Contains("capturing") ? null :
            button.Content as TextBlock;
        if (textBlock != null)
        {
            textBlock.Text = "请按键或鼠标...";
        }

        button.Classes.Add("capturing");

        this.KeyDown += OnKeyDown;
        this.PointerPressed += OnPointerPressed;
        this.PointerWheelChanged += OnPointerWheelChanged;
        this.Focus();

        _captureCallback = onCaptured;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            CancelCapture();
            return;
        }

        if (e.Key == Key.None)
            return;

        if (ShortcutBindings.IsModifierKey(e.Key) || e.Key == Key.System)
            return;

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        CompleteCapture(gesture);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        var updateKind = properties.PointerUpdateKind;

        if (ShortcutBindings.IsBlacklistedMouseButton(updateKind))
        {
            ShowBlacklistedMessage();
            return;
        }

        var gesture = ShortcutRouter.MouseButtonToKeyGesture(updateKind);
        if (gesture != null)
        {
            e.Handled = true;
            CompleteCapture(gesture);
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        ShowBlacklistedMessage();
    }

    private void ShowBlacklistedMessage()
    {
        Console.WriteLine("左键、右键、滚轮中键不支持作为快捷键");
    }

    private void CompleteCapture(KeyGesture gesture)
    {
        if (_capturingButton != null && _captureCallback != null)
        {
            _captureCallback(gesture);

            _capturingButton.Classes.Remove("capturing");
            _capturingButton = null;
            _captureCallback = null;

            this.KeyDown -= OnKeyDown;
            this.PointerPressed -= OnPointerPressed;
            this.PointerWheelChanged -= OnPointerWheelChanged;
        }
    }

    private void CancelCapture()
    {
        if (_capturingButton != null)
        {
            _capturingButton.Classes.Remove("capturing");
            _capturingButton = null;
            _captureCallback = null;

            this.KeyDown -= OnKeyDown;
            this.PointerPressed -= OnPointerPressed;
            this.PointerWheelChanged -= OnPointerWheelChanged;

            UpdateUI();
        }
    }

    private void OnPreferencesWindowClosed(object? sender, EventArgs e)
    {
        this.Closed -= OnPreferencesWindowClosed;

        CommitCurrentDligConfig();

        // 将默认快捷输入同步回 settings
        _settings.DefaultQuickInputs = _defaultQuickInputs
            .Where(s => !string.IsNullOrEmpty(s.Character))
            .Select(s => new QuickInputSlot { Label = s.Label, Character = s.Character })
            .ToList();

        // 自动解决冲突：将所有重复按键的操作置为 None
        _settings.MouseConfig = _settings.MouseConfig.ResolveConflicts();
        if (_settings.MouseConfig.HasConflict())
            _changeKind |= SettingsChangeKind.CanvasMouse;

        if (_changeKind != SettingsChangeKind.None)
        {
            AppSettingsService.Save(_settings);
            _provider.Update(_settings, _changeKind);
            _changeKind = SettingsChangeKind.None;
        }
    }
}
