using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Input;

namespace LabelAva.Services;

[JsonSerializable(typeof(AppSettingsDto))]
[JsonSerializable(typeof(ShortcutBindingsDto))]
[JsonSerializable(typeof(ColorSettingsDto))]
[JsonSerializable(typeof(Models.QuickInputSlot))]
[JsonSerializable(typeof(List<Models.QuickInputSlot>))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppSettingsContext : JsonSerializerContext { }

public static class AppSettingsService
{
    private static readonly string SettingsFile = AppDataHelper.SettingsFilePath;

    public static void Save(Models.AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(new AppSettingsDto(settings), AppSettingsContext.Default.AppSettingsDto);

            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存设置失败: {ex.Message}");
        }
    }

    public static Models.AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return Models.AppSettings.CreateDefaults();
            }

            var json = File.ReadAllText(SettingsFile);

            var dto = JsonSerializer.Deserialize(
                json,
                AppSettingsContext.Default.AppSettingsDto
            );

            if (dto != null)
            {
                var settings = dto.ToSettings();

                // 版本检测：缺失或低于当前版本 → 迁移
                if (settings.Version != Models.AppSettings.CreateDefaults().Version)
                {
                    settings = MigrateSettings(settings);
                    Save(settings);
                }

                return settings;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载设置失败: {ex.Message}");
        }

        return Models.AppSettings.CreateDefaults();
    }

    private static Models.AppSettings MigrateSettings(Models.AppSettings oldSettings)
    {
        var defaults = Models.AppSettings.CreateDefaults();

        // 窗口几何
        defaults.WindowWidth  = oldSettings.WindowWidth;
        defaults.WindowHeight = oldSettings.WindowHeight;
        defaults.WindowX      = oldSettings.WindowX;
        defaults.WindowY      = oldSettings.WindowY;
        defaults.WindowMaximized = oldSettings.WindowMaximized;

        // 用户偏好
        defaults.LabelSize        = oldSettings.LabelSize;
        defaults.AutoFocusTextBox = oldSettings.AutoFocusTextBox;

        // 快捷键（非 null 则深拷贝保留）
        if (oldSettings.Shortcuts != null)
            defaults.Shortcuts = oldSettings.Shortcuts.Clone();

        // 颜色
        if (oldSettings.Colors != null)
            defaults.Colors = oldSettings.Colors.Clone();

        // 连字配置选择
        defaults.ActiveDligConfig = oldSettings.ActiveDligConfig;

        // 鼠标配置（值类型）
        defaults.MouseConfig = oldSettings.MouseConfig;

        // 默认快捷输入（仅旧版有此字段且非空时才拷贝）
        if (oldSettings.DefaultQuickInputs is { Count: > 0 })
            defaults.DefaultQuickInputs = oldSettings.DefaultQuickInputs
                .Select(s => new Models.QuickInputSlot { Label = s.Label ?? "", Character = s.Character ?? "" })
                .ToList();

        return defaults;
    }

    private static Models.CanvasMouseAction ParseMouseAction(string? s, Models.CanvasMouseAction fallback)
        => Enum.TryParse<Models.CanvasMouseAction>(s, out var action) ? action : fallback;
}

public class ColorSettingsDto
{
    public Dictionary<string, string> GroupColors { get; set; } = new();

    public string? SelectedColor { get; set; }

    public ColorSettingsDto() { }

    public ColorSettingsDto(Models.ColorSettings settings)
    {
        GroupColors = settings.GroupColors.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value
        );
        SelectedColor = settings.SelectedColor;
    }

    public Models.ColorSettings ToSettings()
    {
        var settings = new Models.ColorSettings();

        foreach (var kvp in GroupColors)
        {
            if (int.TryParse(kvp.Key, out int groupIndex))
            {
                settings.GroupColors[groupIndex] = kvp.Value;
            }
        }

        settings.SelectedColor = SelectedColor ?? "#33BA90";

        return settings;
    }
}

public class ShortcutBindingsDto
{
    public List<string>? NavigateUp { get; set; }
    public List<string>? NavigateDown { get; set; }
    public List<string>? NavigateUpSecondary { get; set; }
    public List<string>? NavigateDownSecondary { get; set; }
    public List<string>? CopyText { get; set; }
    public List<string>? DeleteLabel { get; set; }
    public List<string>? OpenFile { get; set; }
    public List<string>? SaveFile { get; set; }
    public List<string>? SaveAsFile { get; set; }
    public List<string>? ZoomIn { get; set; }
    public List<string>? ZoomOut { get; set; }
    public List<string>? ResetZoom { get; set; }
    public List<string>? ToggleGroup0 { get; set; }
    public List<string>? ToggleGroup1 { get; set; }
    public List<string>? PageUp { get; set; }
    public List<string>? PageDown { get; set; }

    public ShortcutBindingsDto() { }

    public ShortcutBindingsDto(Models.ShortcutBindings bindings)
    {
        NavigateUp = GestureToList(bindings.NavigateUp);
        NavigateDown = GestureToList(bindings.NavigateDown);
        NavigateUpSecondary = GestureToList(bindings.NavigateUpSecondary);
        NavigateDownSecondary = GestureToList(bindings.NavigateDownSecondary);
        CopyText = GestureToList(bindings.CopyText);
        DeleteLabel = GestureToList(bindings.DeleteLabel);
        OpenFile = GestureToList(bindings.OpenFile);
        SaveFile = GestureToList(bindings.SaveFile);
        SaveAsFile = GestureToList(bindings.SaveAsFile);
        ZoomIn = GestureToList(bindings.ZoomIn);
        ZoomOut = GestureToList(bindings.ZoomOut);
        ResetZoom = GestureToList(bindings.ResetZoom);
        ToggleGroup0 = GestureToList(bindings.ToggleGroup0);
        ToggleGroup1 = GestureToList(bindings.ToggleGroup1);
        PageUp = GestureToList(bindings.PageUp);
        PageDown = GestureToList(bindings.PageDown);
    }

    public Models.ShortcutBindings ToBindings()
    {
        return new Models.ShortcutBindings
        {
            NavigateUp = ListToGesture(NavigateUp),
            NavigateDown = ListToGesture(NavigateDown),
            NavigateUpSecondary = ListToGesture(NavigateUpSecondary),
            NavigateDownSecondary = ListToGesture(NavigateDownSecondary),
            CopyText = ListToGesture(CopyText),
            DeleteLabel = ListToGesture(DeleteLabel),
            OpenFile = ListToGesture(OpenFile),
            SaveFile = ListToGesture(SaveFile),
            SaveAsFile = ListToGesture(SaveAsFile),
            ZoomIn = ListToGesture(ZoomIn),
            ZoomOut = ListToGesture(ZoomOut),
            ResetZoom = ListToGesture(ResetZoom),
            ToggleGroup0 = ListToGesture(ToggleGroup0),
            ToggleGroup1 = ListToGesture(ToggleGroup1),
            PageUp = ListToGesture(PageUp),
            PageDown = ListToGesture(PageDown),
        };
    }

    private static List<string>? GestureToList(KeyGesture? gesture)
    {
        if (gesture == null) return null;

        var parts = new List<string>();
        var key = gesture.Key;
        var modifiers = Models.ShortcutBindings.NormalizeModifiers(key, gesture.KeyModifiers);

        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");

        if (!Models.ShortcutBindings.IsModifierKey(key) && key != Key.None)
            parts.Add(key.ToString());

        return parts.Count > 0 ? parts : null;
    }

    private static KeyGesture? ListToGesture(List<string>? parts)
    {
        if (parts == null || parts.Count == 0) return null;

        try
        {
            var modifiers = KeyModifiers.None;
            var key = Key.None;

            if (parts.Count == 1)
            {
                var mod = parts[0].Trim();
                modifiers = mod switch
                {
                    "Ctrl" => KeyModifiers.Control,
                    "Shift" => KeyModifiers.Shift,
                    "Alt" => KeyModifiers.Alt,
                    "Win" => KeyModifiers.Meta,
                    _ => KeyModifiers.None
                };
                if (modifiers != KeyModifiers.None)
                {
                    return mod switch
                    {
                        "Ctrl" => new KeyGesture(Key.LeftCtrl, KeyModifiers.None),
                        "Shift" => new KeyGesture(Key.LeftShift, KeyModifiers.None),
                        "Alt" => new KeyGesture(Key.LeftAlt, KeyModifiers.None),
                        "Win" => new KeyGesture(Key.LWin, KeyModifiers.None),
                        _ => null
                    };
                }
            }

            for (int i = 0; i < parts.Count - 1; i++)
            {
                var mod = parts[i].Trim();
                modifiers |= mod switch
                {
                    "Ctrl" => KeyModifiers.Control,
                    "Shift" => KeyModifiers.Shift,
                    "Alt" => KeyModifiers.Alt,
                    "Win" => KeyModifiers.Meta,
                    _ => KeyModifiers.None
                };
            }

            var keyStr = parts[^1].Trim();
            key = Enum.TryParse<Key>(keyStr, out var parsedKey) ? parsedKey : Key.None;

            if (key == Key.None && modifiers == KeyModifiers.None) return null;

            return new KeyGesture(key, modifiers);
        }
        catch
        {
            return null;
        }
    }
}

public class AppSettingsDto
{
    public string? Version { get; set; }
    public ShortcutBindingsDto? Shortcuts { get; set; }
    public ColorSettingsDto? Colors { get; set; }
    public int LabelSize { get; set; } = 32;
    public bool AutoFocusTextBox { get; set; } = true;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public bool WindowMaximized { get; set; }
    public string? ActiveDligConfig { get; set; }
    public List<Models.QuickInputSlot>? DefaultQuickInputs { get; set; }
    public string? MouseLeftButton { get; set; }
    public string? MouseMiddleButton { get; set; }
    public string? MouseRightButton { get; set; }

    public AppSettingsDto() { }

    public AppSettingsDto(Models.AppSettings settings)
    {
        Version = settings.Version;
        Shortcuts = new ShortcutBindingsDto(settings.Shortcuts);
        Colors = new ColorSettingsDto(settings.Colors);
        LabelSize = settings.LabelSize;
        AutoFocusTextBox = settings.AutoFocusTextBox;
        WindowWidth = settings.WindowWidth;
        WindowHeight = settings.WindowHeight;
        WindowX = settings.WindowX;
        WindowY = settings.WindowY;
        WindowMaximized = settings.WindowMaximized;
        ActiveDligConfig = settings.ActiveDligConfig;
        DefaultQuickInputs = settings.DefaultQuickInputs
            .Select(s => new Models.QuickInputSlot { Label = s.Label, Character = s.Character })
            .ToList();
        MouseLeftButton = settings.MouseConfig.LeftButton.ToString();
        MouseMiddleButton = settings.MouseConfig.MiddleButton.ToString();
        MouseRightButton = settings.MouseConfig.RightButton.ToString();
    }

    public Models.AppSettings ToSettings()
    {
        var shortcuts = Shortcuts?.ToBindings() ?? Models.ShortcutBindings.CreateDefaults();
        var colors = Colors?.ToSettings() ?? Models.ColorSettings.CreateDefaults();

        return new Models.AppSettings
        {
            Version = Version ?? "0.3.0",
            Shortcuts = shortcuts,
            Colors = colors,
            LabelSize = LabelSize,
            AutoFocusTextBox = AutoFocusTextBox,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            WindowX = WindowX,
            WindowY = WindowY,
            WindowMaximized = WindowMaximized,
            ActiveDligConfig = ActiveDligConfig,
            DefaultQuickInputs = DefaultQuickInputs?
                .Select(s => new Models.QuickInputSlot { Label = s.Label ?? "", Character = s.Character ?? "" })
                .ToList() ?? Models.AppSettings.CreateDefaultQuickInputs(),
            MouseConfig = new Models.CanvasMouseConfig
            {
                LeftButton   = ParseMouseAction(MouseLeftButton,   Models.CanvasMouseAction.AddSelect),
                MiddleButton = ParseMouseAction(MouseMiddleButton, Models.CanvasMouseAction.Pan),
                RightButton  = ParseMouseAction(MouseRightButton,  Models.CanvasMouseAction.Pan),
            },
        };
    }

    private static Models.CanvasMouseAction ParseMouseAction(string? s, Models.CanvasMouseAction fallback)
        => Enum.TryParse<Models.CanvasMouseAction>(s, out var action) ? action : fallback;
}
