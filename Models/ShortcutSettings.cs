using System.Collections.Generic;
using Avalonia.Input;

namespace LabelAva.Models;

[Flags]
public enum SettingsChangeKind
{
    None       = 0,
    Shortcuts  = 1 << 0,
    Colors     = 1 << 1,
    LabelSize  = 1 << 2,
    AutoFocus  = 1 << 3,
    All        = Shortcuts | Colors | LabelSize | AutoFocus,
}

/// <summary>
/// 颜色设置数据模型
/// </summary>
public class ColorSettings
{
    /// <summary>
    /// 分组颜色列表（Key: 分组索引, Value: 十六进制颜色代码如 "#FF0000"）
    /// </summary>
    public Dictionary<int, string> GroupColors { get; set; } = new();

    /// <summary>
    /// 选中高亮颜色（十六进制颜色代码）
    /// </summary>
    public string SelectedColor { get; set; } = "#1E90FF";

    /// <summary>
    /// 创建默认颜色设置
    /// </summary>
    public static ColorSettings CreateDefaults()
    {
        return new ColorSettings
        {
            GroupColors = new Dictionary<int, string>
            {
                { 1, "#E74856" },  // 框内 - 红色
                { 2, "#1E90FF" }   // 框外 - 蓝色
            },
            SelectedColor = "#33BA90" // 蓝绿色
        };
    }

    /// <summary>
    /// 克隆当前颜色设置
    /// </summary>
    public ColorSettings Clone()
    {
        return new ColorSettings
        {
            GroupColors = new Dictionary<int, string>(GroupColors),
            SelectedColor = SelectedColor
        };
    }
}

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

    // 分组切换快捷键
    public KeyGesture? ToggleGroup0 { get; set; }
    public KeyGesture? ToggleGroup1 { get; set; }

    // 颜色设置
    public ColorSettings Colors { get; set; } = ColorSettings.CreateDefaults();

    // 编辑行为设置
    /// <summary>
    /// 是否在选中标签后自动聚焦到文本框
    /// </summary>
    public bool AutoFocusTextBox { get; set; } = true;
    
    // 标签显示设置
    /// <summary>
    /// 标号大小（像素），默认 64
    /// </summary>
    public int LabelSize { get; set; } = 64;
    
    /// <summary>
    /// 检查是否为黑名单中的鼠标按钮（不支持的输入）
    /// 左键、右键、滚轮中键被加入黑名单
    /// 鼠标侧键 XButton1、XButton2 允许作为快捷键
    /// </summary>
    public static bool IsBlacklistedMouseButton(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            // 左键按下/释放 - 黑名单
            PointerUpdateKind.LeftButtonPressed => true,
            PointerUpdateKind.LeftButtonReleased => true,
            // 右键按下/释放 - 黑名单
            PointerUpdateKind.RightButtonPressed => true,
            PointerUpdateKind.RightButtonReleased => true,
            // 中键（滚轮按下）按下/释放 - 黑名单
            PointerUpdateKind.MiddleButtonPressed => true,
            PointerUpdateKind.MiddleButtonReleased => true,
            // 侧键 XButton1、XButton2 - 允许，不在黑名单中
            _ => false
        };
    }
    
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
            ResetZoom = new KeyGesture(Key.D0, KeyModifiers.Control),

            // 分组切换 - Ctrl+1 (框内), Ctrl+2 (框外)
            ToggleGroup0 = new KeyGesture(Key.D1, KeyModifiers.Control),
            ToggleGroup1 = new KeyGesture(Key.D2, KeyModifiers.Control),

            // 颜色设置 - 默认颜色
            Colors = ColorSettings.CreateDefaults(),

            // 标号大小 - 默认 64 像素
            LabelSize = 64
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
            // 功能键 F13-F24（用于映射鼠标侧键）
            Key.F13 => "鼠标侧键1",
            Key.F14 => "鼠标侧键2",
            Key.F15 => "F15",
            Key.F16 => "F16",
            Key.F17 => "F17",
            Key.F18 => "F18",
            Key.F19 => "F19",
            Key.F20 => "F20",
            Key.F21 => "F21",
            Key.F22 => "F22",
            Key.F23 => "F23",
            Key.F24 => "F24",
            _ => key.ToString()
        };
    }
}
