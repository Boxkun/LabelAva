using Avalonia.Input;
using LabelAva.Models;

namespace LabelAva.Services;

public enum ShortcutAction
{
    NavigateUp,
    NavigateDown,
    NavigatePageUp,
    NavigatePageDown,
    CopyText,
    DeleteLabel,
    OpenFile,
    SaveFile,
    SwitchToGroup0,
    SwitchToGroup1,
}

public class ShortcutRouter
{
    private ShortcutBindings _bindings;

    public ShortcutRouter(ShortcutBindings bindings)
    {
        _bindings = bindings;
    }

    public void UpdateSettings(ShortcutBindings bindings) => _bindings = bindings;

    public ShortcutAction? MatchKeyGesture(KeyGesture gesture, bool isTextBoxFocused = false)
    {
        if (!isTextBoxFocused)
        {
            if (_bindings.ToggleGroup0 != null && gesture.Equals(_bindings.ToggleGroup0))
                return ShortcutAction.SwitchToGroup0;
            if (_bindings.ToggleGroup1 != null && gesture.Equals(_bindings.ToggleGroup1))
                return ShortcutAction.SwitchToGroup1;
        }

        if (MatchesNavigateUp(gesture))
            return ShortcutAction.NavigateUp;
        if (MatchesNavigateDown(gesture))
            return ShortcutAction.NavigateDown;

        if (_bindings.PageUp != null && gesture.Equals(_bindings.PageUp))
            return ShortcutAction.NavigatePageUp;
        if (_bindings.PageDown != null && gesture.Equals(_bindings.PageDown))
            return ShortcutAction.NavigatePageDown;

        if (!isTextBoxFocused)
        {
            if (_bindings.DeleteLabel != null && gesture.Equals(_bindings.DeleteLabel))
                return ShortcutAction.DeleteLabel;

            if (_bindings.CopyText != null && gesture.Equals(_bindings.CopyText))
                return ShortcutAction.CopyText;
        }

        if (_bindings.OpenFile != null && gesture.Equals(_bindings.OpenFile))
            return ShortcutAction.OpenFile;
        if (_bindings.SaveFile != null && gesture.Equals(_bindings.SaveFile))
            return ShortcutAction.SaveFile;

        return null;
    }

    public ShortcutAction? MatchPointerUpdate(PointerUpdateKind updateKind)
    {
        var gesture = MouseButtonToKeyGesture(updateKind);
        if (gesture == null) return null;
        return MatchKeyGesture(gesture);
    }

    private bool MatchesNavigateUp(KeyGesture gesture)
    {
        return (_bindings.NavigateUp != null && gesture.Equals(_bindings.NavigateUp)) ||
               (_bindings.NavigateUpSecondary != null && gesture.Equals(_bindings.NavigateUpSecondary));
    }

    private bool MatchesNavigateDown(KeyGesture gesture)
    {
        return (_bindings.NavigateDown != null && gesture.Equals(_bindings.NavigateDown)) ||
               (_bindings.NavigateDownSecondary != null && gesture.Equals(_bindings.NavigateDownSecondary));
    }

    public static KeyGesture? MouseButtonToKeyGesture(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            PointerUpdateKind.XButton1Pressed => new KeyGesture(Key.F13),
            PointerUpdateKind.XButton2Pressed => new KeyGesture(Key.F14),
            _ => null
        };
    }
}
