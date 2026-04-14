using Avalonia.Input;
using LabelAva.Models;

namespace LabelAva.Services;

/// <summary>
/// 快捷键动作类型
/// </summary>
public enum ShortcutAction
{
    NavigateUp,
    NavigateDown,
    CopyText,
    DeleteLabel,
    OpenFile,
    SaveFile,
    SwitchToGroup0,
    SwitchToGroup1,
}

/// <summary>
/// 快捷键路由服务：将输入手势匹配为 ShortcutAction。
/// 纯匹配逻辑，不执行任何副作用。
/// </summary>
public class ShortcutRouter
{
    private ShortcutSettings _settings;

    public ShortcutRouter(ShortcutSettings settings)
    {
        _settings = settings;
    }

    /// <summary>更新快捷键设置（设置变更时调用）</summary>
    public void UpdateSettings(ShortcutSettings settings) => _settings = settings;

    /// <summary>
    /// 从 KeyGesture 匹配快捷键动作
    /// </summary>
    /// <param name="gesture">当前按键手势</param>
    /// <param name="isTextBoxFocused">TextBox 是否有焦点（影响分组切换快捷键）</param>
    /// <returns>匹配到的动作，未匹配返回 null</returns>
    public ShortcutAction? MatchKeyGesture(KeyGesture gesture, bool isTextBoxFocused = false)
    {
        // 分组切换：TextBox 有焦点时不触发
        if (!isTextBoxFocused)
        {
            if (_settings.ToggleGroup0 != null && gesture.Equals(_settings.ToggleGroup0))
                return ShortcutAction.SwitchToGroup0;
            if (_settings.ToggleGroup1 != null && gesture.Equals(_settings.ToggleGroup1))
                return ShortcutAction.SwitchToGroup1;
        }

        // 导航
        if (MatchesNavigateUp(gesture))
            return ShortcutAction.NavigateUp;
        if (MatchesNavigateDown(gesture))
            return ShortcutAction.NavigateDown;

        if (!isTextBoxFocused)
        {
            if (_settings.DeleteLabel != null && gesture.Equals(_settings.DeleteLabel))
                return ShortcutAction.DeleteLabel;
        }

        if (_settings.CopyText != null && gesture.Equals(_settings.CopyText))
            return ShortcutAction.CopyText;

        if (_settings.OpenFile != null && gesture.Equals(_settings.OpenFile))
            return ShortcutAction.OpenFile;
        if (_settings.SaveFile != null && gesture.Equals(_settings.SaveFile))
            return ShortcutAction.SaveFile;

        return null;
    }

    /// <summary>
    /// 从鼠标侧键 PointerUpdateKind 匹配快捷键动作
    /// </summary>
    public ShortcutAction? MatchPointerUpdate(PointerUpdateKind updateKind)
    {
        var gesture = MouseButtonToKeyGesture(updateKind);
        if (gesture == null) return null;
        return MatchKeyGesture(gesture);
    }

    private bool MatchesNavigateUp(KeyGesture gesture)
    {
        return (_settings.NavigateUp != null && gesture.Equals(_settings.NavigateUp)) ||
               (_settings.NavigateUpSecondary != null && gesture.Equals(_settings.NavigateUpSecondary));
    }

    private bool MatchesNavigateDown(KeyGesture gesture)
    {
        return (_settings.NavigateDown != null && gesture.Equals(_settings.NavigateDown)) ||
               (_settings.NavigateDownSecondary != null && gesture.Equals(_settings.NavigateDownSecondary));
    }

    internal static KeyGesture? MouseButtonToKeyGesture(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            PointerUpdateKind.XButton1Pressed => new KeyGesture(Key.F13),  // 鼠标侧键1
            PointerUpdateKind.XButton2Pressed => new KeyGesture(Key.F14),  // 鼠标侧键2
            _ => null
        };
    }
}
