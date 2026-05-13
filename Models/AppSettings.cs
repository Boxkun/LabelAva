using System.Collections.Generic;
using Avalonia.Input;
using LabelAva.Services;

namespace LabelAva.Models;

[Flags]
public enum SettingsChangeKind
{
    None       = 0,
    Shortcuts  = 1 << 0,
    Colors     = 1 << 1,
    LabelSize  = 1 << 2,
    AutoFocus  = 1 << 3,
    DligConfig = 1 << 4,
    All        = Shortcuts | Colors | LabelSize | AutoFocus | DligConfig,
}

public class ShortcutBindings
{
    public KeyGesture? NavigateUp { get; set; }
    public KeyGesture? NavigateDown { get; set; }
    public KeyGesture? NavigateUpSecondary { get; set; }
    public KeyGesture? NavigateDownSecondary { get; set; }
    public KeyGesture? CopyText { get; set; }
    public KeyGesture? DeleteLabel { get; set; }
    public KeyGesture? OpenFile { get; set; }
    public KeyGesture? SaveFile { get; set; }
    public KeyGesture? ZoomIn { get; set; }
    public KeyGesture? ZoomOut { get; set; }
    public KeyGesture? ResetZoom { get; set; }
    public KeyGesture? ToggleGroup0 { get; set; }
    public KeyGesture? ToggleGroup1 { get; set; }
    public KeyGesture? PageUp { get; set; }
    public KeyGesture? PageDown { get; set; }

    public static ShortcutBindings CreateDefaults()
    {
        return new ShortcutBindings
        {
            NavigateUp = new KeyGesture(Key.Up),
            NavigateDown = new KeyGesture(Key.Down),
            CopyText = new KeyGesture(Key.C, KeyModifiers.Control),
            DeleteLabel = new KeyGesture(Key.Delete),
            OpenFile = new KeyGesture(Key.O, KeyModifiers.Control),
            SaveFile = new KeyGesture(Key.S, KeyModifiers.Control),
            ZoomIn = new KeyGesture(Key.OemPlus, KeyModifiers.Control),
            ZoomOut = new KeyGesture(Key.OemMinus, KeyModifiers.Control),
            ResetZoom = new KeyGesture(Key.D0, KeyModifiers.Control),
            ToggleGroup0 = new KeyGesture(Key.D1, KeyModifiers.Control),
            ToggleGroup1 = new KeyGesture(Key.D2, KeyModifiers.Control),
            PageUp = new KeyGesture(Key.PageUp),
            PageDown = new KeyGesture(Key.PageDown),
        };
    }

    public static string KeyGestureToString(KeyGesture? gesture)
    {
        if (gesture == null)
            return "未设置";

        var parts = new List<string>();
        var modifiers = gesture.KeyModifiers;
        var key = gesture.Key;

        var normalizedModifiers = NormalizeModifiers(key, modifiers);
        bool isMac = PlatformHelper.IsMacOS;

        if (normalizedModifiers.HasFlag(KeyModifiers.Control))
            parts.Add(isMac ? "⌘" : "Ctrl");
        if (normalizedModifiers.HasFlag(KeyModifiers.Shift))
            parts.Add(isMac ? "⇧" : "Shift");
        if (normalizedModifiers.HasFlag(KeyModifiers.Alt))
            parts.Add(isMac ? "⌥" : "Alt");
        if (normalizedModifiers.HasFlag(KeyModifiers.Meta))
            parts.Add(isMac ? "⌃" : "Win");

        if (!IsModifierKey(key) && key != Key.None)
        {
            parts.Add(GetKeyDisplayName(key));
        }

        return string.Join(" + ", parts);
    }

    public static KeyModifiers NormalizeModifiers(Key key, KeyModifiers modifiers)
    {
        var result = modifiers;

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

    public static bool IsBlacklistedMouseButton(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => true,
            PointerUpdateKind.LeftButtonReleased => true,
            PointerUpdateKind.RightButtonPressed => true,
            PointerUpdateKind.RightButtonReleased => true,
            PointerUpdateKind.MiddleButtonPressed => true,
            PointerUpdateKind.MiddleButtonReleased => true,
            _ => false
        };
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.LeftCtrl => "Ctrl",
            Key.RightCtrl => "Ctrl",
            Key.LeftShift => "Shift",
            Key.RightShift => "Shift",
            Key.LeftAlt => "Alt",
            Key.RightAlt => "Alt",
            Key.LWin => PlatformHelper.IsMacOS ? "⌘" : "Win",
            Key.RWin => PlatformHelper.IsMacOS ? "⌘" : "Win",
            Key.Up => "上箭头",
            Key.Down => "下箭头",
            Key.Left => "左箭头",
            Key.Right => "右箭头",
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

public class ColorSettings
{
    public Dictionary<int, string> GroupColors { get; set; } = new();

    public string SelectedColor { get; set; } = "#33BA90";

    public static ColorSettings CreateDefaults()
    {
        return new ColorSettings
        {
            GroupColors = new Dictionary<int, string>
            {
                { 1, "#E74856" },
                { 2, "#1E90FF" }
            },
            SelectedColor = "#33BA90"
        };
    }

    public ColorSettings Clone()
    {
        return new ColorSettings
        {
            GroupColors = new Dictionary<int, string>(GroupColors),
            SelectedColor = SelectedColor
        };
    }
}

public class AppSettings
{
    public ShortcutBindings Shortcuts { get; set; } = ShortcutBindings.CreateDefaults();
    public ColorSettings Colors { get; set; } = ColorSettings.CreateDefaults();
    public int LabelSize { get; set; } = 32;
    public bool AutoFocusTextBox { get; set; } = true;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public bool WindowMaximized { get; set; }
    public string? ActiveDligConfig { get; set; }

    public static AppSettings CreateDefaults()
    {
        return new AppSettings
        {
            Shortcuts = ShortcutBindings.CreateDefaults(),
            Colors = ColorSettings.CreateDefaults(),
            LabelSize = 32,
            AutoFocusTextBox = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            WindowX = -1,
            WindowY = -1,
            WindowMaximized = false,
            ActiveDligConfig = null,
        };
    }
}
