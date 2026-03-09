using Avalonia.Input;

namespace LabelAva.Models;

/// <summary>
/// 快捷键设置数据模型
/// </summary>
public class ShortcutSettings
{
    // 导航快捷键 - 主要
    public KeyGesture? NavigateUp { get; set; }
    public KeyGesture? NavigateDown { get; set; }
    
    // 导航快捷键 - 次要
    public KeyGesture? NavigateUpSecondary { get; set; }
    public KeyGesture? NavigateDownSecondary { get; set; }
    
    // 复制快捷键
    public KeyGesture? CopyText { get; set; }
    
    // 缩放快捷键
    public KeyGesture? ZoomIn { get; set; }
    public KeyGesture? ZoomOut { get; set; }
    public KeyGesture? ResetZoom { get; set; }
    
    /// <summary>
    /// 默认快捷键设置
    /// </summary>
    public static ShortcutSettings CreateDefaults()
    {
        return new ShortcutSettings
        {
            // 导航 - 上: UpArrow, 下: DownArrow
            NavigateUp = new KeyGesture(Key.Up),
            NavigateDown = new KeyGesture(Key.Down),
            
            // 复制 - Ctrl+C
            CopyText = new KeyGesture(Key.C, KeyModifiers.Control),
            
            // 缩放 - Ctrl++/= (放大), Ctrl+- (缩小), Ctrl+0 (重置)
            ZoomIn = new KeyGesture(Key.OemPlus, KeyModifiers.Control),
            ZoomOut = new KeyGesture(Key.OemMinus, KeyModifiers.Control),
            ResetZoom = new KeyGesture(Key.D0, KeyModifiers.Control)
        };
    }
    
    /// <summary>
    /// 从 KeyGesture 获取可读字符串
    /// </summary>
    public static string KeyGestureToString(KeyGesture? gesture)
    {
        if (gesture == null)
            return "未设置";
        
        var parts = new List<string>();
        var modifiers = gesture.KeyModifiers;
        var key = gesture.Key;
        
        // 标准化修饰键：将 LeftCtrl -> Ctrl, RightCtrl -> Ctrl 等
        var normalizedModifiers = NormalizeModifiers(key, modifiers);
        
        // 排除修饰键本身，只显示 Ctrl、Shift、Alt、Win
        if (normalizedModifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (normalizedModifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (normalizedModifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (normalizedModifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");
        
        // 添加主键（标准化后），避免修饰键重复显示（如 "Ctrl + Ctrl"）
        if (!IsModifierKey(key) && key != Key.None)
        {
            parts.Add(GetKeyDisplayName(key));
        }
        
        return string.Join(" + ", parts);
    }
    
    /// <summary>
    /// 标准化修饰键：如果 Key 本身是修饰键（如 LeftCtrl），则将其从 Key 移到 modifiers
    /// </summary>
    private static KeyModifiers NormalizeModifiers(Key key, KeyModifiers modifiers)
    {
        var result = modifiers;
        
        // 如果 Key 本身就是修饰键，将其添加到 modifiers
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                result |= KeyModifiers.Control;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                result |= KeyModifiers.Shift;
                break;
            case Key.LeftAlt:
            case Key.RightAlt:
                result |= KeyModifiers.Alt;
                break;
            case Key.LWin:
            case Key.RWin:
                result |= KeyModifiers.Meta;
                break;
        }
        
        return result;
    }
    
    /// <summary>
    /// 检查是否为修饰键
    /// </summary>
    public static bool IsModifierKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl => true,
            Key.RightCtrl => true,
            Key.LeftShift => true,
            Key.RightShift => true,
            Key.LeftAlt => true,
            Key.RightAlt => true,
            Key.LWin => true,
            Key.RWin => true,
            _ => false
        };
    }
    
    /// <summary>
    /// 获取按键的显示名称
    /// </summary>
    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            // 修饰键显示名称
            Key.LeftCtrl => "Ctrl",
            Key.RightCtrl => "Ctrl",
            Key.LeftShift => "Shift",
            Key.RightShift => "Shift",
            Key.LeftAlt => "Alt",
            Key.RightAlt => "Alt",
            Key.LWin => "Win",
            Key.RWin => "Win",
            // 方向键
            Key.Up => "上箭头",
            Key.Down => "下箭头",
            Key.Left => "左箭头",
            Key.Right => "右箭头",
            // 其他键
            Key.Space => "空格",
            Key.Enter => "回车",
            Key.Escape => "Esc",
            Key.Tab => "Tab",
            Key.Back => "退格",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Insert => "Insert",
            Key.F1 => "F1",
            Key.F2 => "F2",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F6 => "F6",
            Key.F7 => "F7",
            Key.F8 => "F8",
            Key.F9 => "F9",
            Key.F10 => "F10",
            Key.F11 => "F11",
            Key.F12 => "F12",
            Key.OemPlus => "=",
            Key.OemMinus => "-",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            Key.OemQuestion => "/",
            Key.OemTilde => "`",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            _ => key.ToString()
        };
    }
}
