using System;
using System.IO;
using System.Text.Json;
using Avalonia.Input;

namespace LabelAva.Services;

/// <summary>
/// 快捷键设置持久化服务
/// </summary>
public static class ShortcutSettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelAva");
    
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "shortcuts.json");
    
    /// <summary>
    /// 保存快捷键设置
    /// </summary>
    public static void Save(Models.ShortcutSettings settings)
    {
        try
        {
            // 确保目录存在
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            
            var json = JsonSerializer.Serialize(new ShortcutSettingsDto(settings), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
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
            var dto = JsonSerializer.Deserialize<ShortcutSettingsDto>(json);
            
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
    }
    
    public Models.ShortcutSettings ToSettings()
    {
        return new Models.ShortcutSettings
        {
            NavigateUp = StringToGesture(NavigateUp),
            NavigateDown = StringToGesture(NavigateDown),
            NavigateUpSecondary = StringToGesture(NavigateUpSecondary),
            NavigateDownSecondary = StringToGesture(NavigateDownSecondary),
            CopyText = StringToGesture(CopyText),
            ZoomIn = StringToGesture(ZoomIn),
            ZoomOut = StringToGesture(ZoomOut),
            ResetZoom = StringToGesture(ResetZoom)
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
