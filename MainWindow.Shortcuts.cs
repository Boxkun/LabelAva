using Avalonia.Controls;
using Avalonia.Input;
using LabelAva.Services;
using LabelAva.Models;
using LabelAva.ViewModels;


namespace LabelAva;

/// <summary>
/// MainWindow 的「Shortcuts」部分。
/// 全局快捷键路由与执行，以及编辑模式守卫。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 全局快捷键拦截（隧道路由，在子控件处理前触发）
    /// 用于接管并统一处理 TextBox 等子控件的撤销/重做快捷键冲突
    /// </summary>
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // 【防卫】初始化未完成前只处理 Ctrl+Enter（不依赖任何 VM）
        if (!_isInitialized)
        {
            var modifiersEarly = e.KeyModifiers;
            bool isCtrlPressedEarly = (modifiersEarly & KeyModifiers.Control) != 0 || (modifiersEarly & KeyModifiers.Meta) != 0;
            if (isCtrlPressedEarly && (e.Key == Key.Return || e.Key == Key.Enter))
            {
                if (_translationTextBox != null && _translationTextBox.IsFocused)
                {
                    e.Handled = true;
                }
            }
            return;
        }

        var modifiers = e.KeyModifiers;
        // 兼容 Windows (Control) 和 Mac (Meta)
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;
        bool isShiftPressed = (modifiers & KeyModifiers.Shift) != 0;

        // ========== 撤销/重做：通过隧道拦截，手动路由到 HistoryViewModel Command ==========
        // 必须在隧道阶段拦截，防止 TextBox 等原生控件触发内置撤销逻辑
        if (isCtrlPressed && e.Key == Key.Z)
        {
            if (!RequireEditMode()) { e.Handled = true; return; }
            if (isShiftPressed)
            {
                ViewModel.History.RedoCommand.Execute(null);
            }
            else
            {
                ViewModel.History.UndoCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (isCtrlPressed && e.Key == Key.Y)
        {
            if (!RequireEditMode()) { e.Handled = true; return; }
            ViewModel.History.RedoCommand.Execute(null);
            e.Handled = true;
        }
        
        // ↑↓ 方向键：仅拦截纯方向键（无修饰键），文本框聚焦时放行，其他情况吞噬
        bool isTextBoxFocused = _translationTextBox != null && _translationTextBox.IsFocused;
        bool isPlainArrow = (e.Key == Key.Up || e.Key == Key.Down) && e.KeyModifiers == KeyModifiers.None;
        if (isPlainArrow && !isTextBoxFocused)
        {
            e.Handled = true;
            return;
        }
        
        // 通过 ShortcutRouter 匹配可配置快捷键
        var currentGesture = new KeyGesture(e.Key, e.KeyModifiers);
        var action = _shortcutRouter.MatchKeyGesture(currentGesture, isTextBoxFocused);
        if (action.HasValue)
        {
            ExecuteShortcutAction(action.Value);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 执行快捷键动作（由 ShortcutRouter 匹配后统一调用）
    /// </summary>
    private void ExecuteShortcutAction(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.NavigateUp:
                _isKeyboardNavigation = true;
                Navigation.NavigateUpCommand.Execute(null);
                break;
            case ShortcutAction.NavigateDown:
                _isKeyboardNavigation = true;
                Navigation.NavigateDownCommand.Execute(null);
                break;
            case ShortcutAction.NavigatePageUp:
                _isKeyboardNavigation = true;
                Navigation.NavigatePageUpCommand.Execute(null);
                break;
            case ShortcutAction.NavigatePageDown:
                _isKeyboardNavigation = true;
                Navigation.NavigatePageDownCommand.Execute(null);
                break;
            case ShortcutAction.CopyText:
                if (Navigation.SelectedItem is TranslationTreeItem item)
                {
                    CopyToClipboard(item.Text);
                    StatusBar.UpdateStatus($"已复制: {SanitizeStatusText(item.Text)}", StatusBarViewModel.StatusType.Info);
                }
                break;
            case ShortcutAction.DeleteLabel:
                if (!RequireEditMode()) break;
                DeleteSelectedLabel();
                break;
            case ShortcutAction.OpenFile:
                ViewModel.Document.OpenCommand.Execute(null);
                break;
            case ShortcutAction.SaveFile:
                ViewModel.Document.SaveCommand.Execute(null);
                break;
            case ShortcutAction.SaveAsFile:
                ViewModel.Document.SaveAsCommand.Execute(null);
                break;
            case ShortcutAction.SwitchToGroup0:
                if (!Edit.IsEditMode) break;
                Edit.SwitchGroupCommand.Execute(0);
                break;
            case ShortcutAction.SwitchToGroup1:
                if (!Edit.IsEditMode) break;
                Edit.SwitchGroupCommand.Execute(1);
                break;
        }
    }

    /// <summary>
    /// 检查当前是否在编辑模式。查看模式下禁止一切修改 TranslationData 的操作。
    /// 所有数据变更入口应统一调用此方法，防止非编辑模式下意外修改数据。
    /// </summary>
    private bool RequireEditMode()
    {
        if (Edit.IsEditMode) return true;
        StatusBar.UpdateStatus("请先切换到编辑模式", StatusBarViewModel.StatusType.Warn);
        return false;
    }

}
