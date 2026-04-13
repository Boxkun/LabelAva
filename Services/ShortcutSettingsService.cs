using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Input;

namespace LabelAva.Services;

/// <summary>
/// AOT-友好的 JSON 序列化上下文
/// Source Generator 在编译时生成序列化代码，支持 AOT 和 Trimming
/// </summary>
[JsonSerializable(typeof(ShortcutSettingsDto))]
[JsonSerializable(typeof(ColorSettingsDto))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class ShortcutSettingsContext : JsonSerializerContext { }

/// <summary>
/// 快捷键设置持久化服务
/// 
/// 【绿色软件规则】
/// 所有配置文件与可执行文件保存在同一目录，便于程序整体复制移动。
/// 配置目录：与入口程序集(.exe)相同目录下的 config.json
/// </summary>
public static class ShortcutSettingsService
{
    // 【绿色软件规则】配置保存在可执行文件同一目录
    private static readonly string ConfigFolder = AppContext.BaseDirectory;
    
    private static readonly string SettingsFile = Path.Combine(ConfigFolder, "config.json");
    
    /// <summary>
    /// 保存快捷键设置
    /// </summary>
    public static void Save(Models.ShortcutSettings settings)
    {
        try
        {
            // 【绿色软件规则】配置保存在可执行文件同一目录，无需额外创建目录
            
            var json = JsonSerializer.Serialize(
                new ShortcutSettingsDto(settings), 
                ShortcutSettingsContext.Default.ShortcutSettingsDto
            );
            
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存快捷键设置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载快捷键设置
    /// </summary>
    public static Models.ShortcutSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return Models.ShortcutSettings.CreateDefaults();
            }
            
            var json = File.ReadAllText(SettingsFile);
            var dto = JsonSerializer.Deserialize(
                json, 
                ShortcutSettingsContext.Default.ShortcutSettingsDto
            );
            
            if (dto != null)
            {
                return dto.ToSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载快捷键设置失败: {ex.Message}");
        }
        
        return Models.ShortcutSettings.CreateDefaults();
    }
}

/// <summary>
/// 用于 JSON 序列化的颜色设置 DTO
/// </summary>
public class ColorSettingsDto
{
    /// <summary>
    /// 分组颜色字典（字符串格式以便JSON序列化）
    /// </summary>
    public Dictionary<string, string> GroupColors { get; set; } = new();

    /// <summary>
    /// 选中高亮颜色
    /// </summary>
    public string? SelectedColor { get; set; }

    public ColorSettingsDto() { }

    public ColorSettingsDto(Models.ColorSettings settings)
    {
        // 将int key转换为string key以便JSON序列化
        GroupColors = settings.GroupColors.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value
        );
        SelectedColor = settings.SelectedColor;
    }

    public Models.ColorSettings ToSettings()
    {
        var settings = new Models.ColorSettings();

        // 转换字符串key回int key
        foreach (var kvp in GroupColors)
        {
            if (int.TryParse(kvp.Key, out int groupIndex))
            {
                settings.GroupColors[groupIndex] = kvp.Value;
            }
        }

        settings.SelectedColor = SelectedColor ?? "#1E90FF";

        return settings;
    }
}

/// <summary>
/// 用于 JSON 序列化的 DTO
/// </summary>
public class ShortcutSettingsDto
{
    // 导航快捷键 - 主要
    public string? NavigateUp { get; set; }
    public string? NavigateDown { get; set; }

    // 导航快捷键 - 次要
    public string? NavigateUpSecondary { get; set; }
    public string? NavigateDownSecondary { get; set; }

    // 复制快捷键
    public string? CopyText { get; set; }

    // 缩放快捷键
    public string? ZoomIn { get; set; }
    public string? ZoomOut { get; set; }
    public string? ResetZoom { get; set; }

    // 分组切换快捷键
    public string? ToggleGroup0 { get; set; }
    public string? ToggleGroup1 { get; set; }

    // 颜色设置
    public ColorSettingsDto? Colors { get; set; }

    // 编辑行为设置
    public bool AutoFocusTextBox { get; set; } = true;

    public ShortcutSettingsDto() { }

    public ShortcutSettingsDto(Models.ShortcutSettings settings)
    {
        NavigateUp = GestureToString(settings.NavigateUp);
        NavigateDown = GestureToString(settings.NavigateDown);
        NavigateUpSecondary = GestureToString(settings.NavigateUpSecondary);
        NavigateDownSecondary = GestureToString(settings.NavigateDownSecondary);
        CopyText = GestureToString(settings.CopyText);
        ZoomIn = GestureToString(settings.ZoomIn);
        ZoomOut = GestureToString(settings.ZoomOut);
        ResetZoom = GestureToString(settings.ResetZoom);
        ToggleGroup0 = GestureToString(settings.ToggleGroup0);
        ToggleGroup1 = GestureToString(settings.ToggleGroup1);
        Colors = new ColorSettingsDto(settings.Colors);
        AutoFocusTextBox = settings.AutoFocusTextBox;
    }

    public Models.ShortcutSettings ToSettings()
    {
        var colors = Colors?.ToSettings() ?? Models.ColorSettings.CreateDefaults();

        return new Models.ShortcutSettings
        {
            NavigateUp = StringToGesture(NavigateUp),
            NavigateDown = StringToGesture(NavigateDown),
            NavigateUpSecondary = StringToGesture(NavigateUpSecondary),
            NavigateDownSecondary = StringToGesture(NavigateDownSecondary),
            CopyText = StringToGesture(CopyText),
            ZoomIn = StringToGesture(ZoomIn),
            ZoomOut = StringToGesture(ZoomOut),
            ResetZoom = StringToGesture(ResetZoom),
            ToggleGroup0 = StringToGesture(ToggleGroup0),
            ToggleGroup1 = StringToGesture(ToggleGroup1),
            Colors = colors,
            AutoFocusTextBox = AutoFocusTextBox
        };
    }
    
    private static string? GestureToString(KeyGesture? gesture)
    {
        if (gesture == null) return null;
        
        var parts = new List<string>();
        var key = gesture.Key;
        var modifiers = gesture.KeyModifiers;
        
        // 标准化：如果 Key 是修饰键（单独按下，没有其他主键），添加到 modifiers 并清除主键
        // 但如果同时有其他主键（如 Ctrl+C），则保留主键
        if (key == Key.LeftCtrl || key == Key.RightCtrl)
        {
            modifiers |= KeyModifiers.Control;
            if (modifiers == KeyModifiers.Control) // 只有 Ctrl，没有其他主键
                key = Key.None;
        }
        else if (key == Key.LeftShift || key == Key.RightShift)
        {
            modifiers |= KeyModifiers.Shift;
            if (modifiers == KeyModifiers.Shift)
                key = Key.None;
        }
        else if (key == Key.LeftAlt || key == Key.RightAlt)
        {
            modifiers |= KeyModifiers.Alt;
            if (modifiers == KeyModifiers.Alt)
                key = Key.None;
        }
        else if (key == Key.LWin || key == Key.RWin)
        {
            modifiers |= KeyModifiers.Meta;
            if (modifiers == KeyModifiers.Meta)
                key = Key.None;
        }
        
        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");
        
        // 只有当 key 不是 None 时才添加
        if (key != Key.None)
            parts.Add(key.ToString());
        
        return string.Join("+", parts);
    }
    
    private static KeyGesture? StringToGesture(string? str)
    {
        if (string.IsNullOrEmpty(str)) return null;
        
        try
        {
            var parts = str.Split('+');
            if (parts.Length < 1) return null;
            
            var modifiers = KeyModifiers.None;
            var key = Key.None;
            
            // 如果只有一个部分且它是修饰键名称，则创建修饰键-only 手势
            if (parts.Length == 1)
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
                    // 修饰键-only: 使用 Key 作为主键来存储
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
            
            for (int i = 0; i < parts.Length - 1; i++)
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
            
            // 允许 key 为 None 的情况（当只有修饰键如 "Ctrl" 时）
            if (key == Key.None && modifiers == KeyModifiers.None) return null;
            
            return new KeyGesture(key, modifiers);
        }
        catch
        {
            return null;
        }
    }
}
