using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LabelAva.Services;
using LabelAva.Models;


namespace LabelAva;

/// <summary>
/// MainWindow 的「EditPanel」部分。
/// 编辑区：视图重建、分组切换按钮、快捷输入工具栏、翻译文本框的按键/文本/焦点处理。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 重建当前视图以同步 UI（用于 Undo/Redo 后刷新界面）
    /// </summary>
    private void RebuildCurrentView()
    {
        if (Document.TranslationData == null)
            return;
        
        // 在重建前记住当前选中的标签索引
        int? previouslySelectedLabelIndex = null;
        if (Navigation.SelectedItem is TranslationTreeItem currentItem)
        {
            previouslySelectedLabelIndex = currentItem.Index;
        }
        
        // 【新增】如果有待选中的新标签（添加标签操作），优先使用它
        if (CanvasWorkspace.PendingNewLabelIndex.HasValue)
        {
            previouslySelectedLabelIndex = CanvasWorkspace.PendingNewLabelIndex;
        }
        
        // 重新构建树视图
        Navigation.BuildTreeView(Document.TranslationData);

        // 不清空 TextBox，保留用户正在编辑的内容
        // TextBox 的值会在后续的 SelectionChanged 中被正确设置

        // ======================= FIX START =======================
        // 更新画布标注
        // 【修复核心】：如果当前正在按下鼠标拖拽某个标签，则跳过全量图形销毁与重建，
        // 保护当前正在捕获鼠标事件的原生控件不被销毁，从而保持拖拽的连续性。
        if (!CanvasControl.IsDraggingLabel)
        {
            UpdateLabels();
        }
        else
        {
            // 如果跳过重建，也要确保同步其可能因选中项变化带来的高亮状态
            if (previouslySelectedLabelIndex.HasValue)
            {
                CanvasControl.HighlightLabel(previouslySelectedLabelIndex.Value);
            }
        }
        // ======================= FIX END =======================
        
        // 尝试恢复当前选中的图片
        if (!string.IsNullOrEmpty(CanvasControl.CurrentImagePath))
        {
            var imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
            var treeItem = Navigation.FindTreeItemByImageName(imageName);
            if (treeItem != null)
            {
                Navigation.CurrentTreeItem = treeItem;
                treeItem.IsExpanded = true;

                if (previouslySelectedLabelIndex.HasValue)
                {
                    // 恢复焦点到特定的标签项
                    var labelItem = treeItem.Translations.FirstOrDefault(t => t.Index == previouslySelectedLabelIndex.Value);
                    if (labelItem != null)
                    {
                        Navigation.SelectedItem = labelItem;
                    }
                    else
                    {
                        Navigation.SelectedItem = treeItem;
                    }
                }
                else
                {
                    Navigation.SelectedItem = treeItem;
                }
            }
        }
        
        // 【新增】如果有待选中的新标签已被选中，聚焦到文本框
        // 根据设置决定是否自动聚焦
        if (CanvasWorkspace.PendingNewLabelIndex.HasValue && _settingsProvider.Current.AutoFocusTextBox)
        {
            // 清除待选中状态后，聚焦到文本框
            CanvasWorkspace.ClearPendingNewLabelIndex();

            // 延迟聚焦到文本框，确保 UI 已完成重建
            Dispatcher.UIThread.Post(() =>
            {
                if (_translationTextBox is not { IsEnabled: true }) return;

                var len = _translationTextBox.Text?.Length ?? 0;
                if (len > 0)
                {
                    _translationTextBox.CaretIndex = 0;
                    _translationTextBox.CaretIndex = len;
                    _translationTextBox.SelectionStart = len;
                    _translationTextBox.SelectionEnd = len;
                }
                _translationTextBox.Focus();
            }, DispatcherPriority.Loaded);
        }
        else if (CanvasWorkspace.PendingNewLabelIndex.HasValue)
        {
            // 如果不需要自动聚焦，仅清除待选中状态
            CanvasWorkspace.ClearPendingNewLabelIndex();
        }
        
    }
    
    /// <summary>
    /// 编辑模式变更事件处理（由 EditViewModel.EditModeChanged 触发）
    /// </summary>
    private void OnEditModeChanged(object? sender, EventArgs e)
    {
        CanvasControl.IsEditMode = Edit.IsEditMode;
        if (Edit.IsEditMode)
        {
            UpdateGroupButtonColors();
            // ApplyDligConfig 已移入选中标记时的光标链（Loaded→Render），避免此项与光标竞态

            // 为连字配置按钮添加系统强调色底色（面板刚变为可见，需延迟等布局完成）
            ApplyDligButtonAccentBackground();
        }
    }

    /// <summary>
    /// 分组变更事件处理（由 EditViewModel.GroupChanged 触发）
    /// </summary>
    private void OnGroupChanged(object? sender, EventArgs e)
    {
        // 同步 RadioButton 选中状态
        var groupIndex = Edit.CurrentGroupIndex;
        if (groupIndex == 0 && Group0RadioButton != null)
            Group0RadioButton.IsChecked = true;
        else if (groupIndex == 1 && Group1RadioButton != null)
            Group1RadioButton.IsChecked = true;
        
        // 统一更新分组按钮颜色
        UpdateGroupButtonColors();
    }

    /// <summary>
    /// 快捷输入工具栏滚轮：把纵向滚轮换算成工具栏的横向平移。
    /// </summary>
    private void OnToolbarScrollWheel(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.Extent.Width > sv.Viewport.Width)
        {
            var offset = sv.Offset;
            var newX = offset.X - e.Delta.Y * 20;
            newX = Math.Max(0, Math.Min(newX, sv.Extent.Width - sv.Viewport.Width));
            sv.Offset = new Vector(newX, offset.Y);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 快捷输入按钮点击事件：把按钮对应的字符写入当前选中标记的文本。
    /// 文本框有选区时替换选中内容，无选区时在光标处插入（对齐 TextBox 的原生输入行为）。
    /// </summary>
    private void OnQuickInputButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not QuickInputSlot slot) return;
        if (string.IsNullOrEmpty(slot.Character)) return;
        if (!RequireEditMode()) return;
        if (Navigation.SelectedTranslationItem is not { } item) return;
        if (_translationTextBox is not { IsEnabled: true }) return;

        var current = item.Text ?? "";

        // 取选区范围；无选区（起点等于终点）时退化为光标处插入
        var selStart = Math.Min(_translationTextBox.SelectionStart, _translationTextBox.SelectionEnd);
        var selEnd = Math.Max(_translationTextBox.SelectionStart, _translationTextBox.SelectionEnd);
        if (selEnd <= selStart)
            selStart = selEnd = _translationTextBox.CaretIndex;

        // 防御：与模型文本长度不一致时收敛到合法区间，避免 Remove/Insert 越界
        selStart = Math.Clamp(selStart, 0, current.Length);
        selEnd = Math.Clamp(selEnd, 0, current.Length);

        item.Text = current.Remove(selStart, selEnd - selStart).Insert(selStart, slot.Character);

        var newCaretPos = selStart + slot.Character.Length;
        _translationTextBox.CaretIndex = newCaretPos;
        _translationTextBox.Focus();
        // TextChanged 事件已推入历史栈并触发 RebuildCurrentView；Background 修复光标位置
        Dispatcher.UIThread.Post(() =>
        {
            if (_translationTextBox is not { IsEnabled: true }) return;
            _translationTextBox.CaretIndex = newCaretPos;
            _translationTextBox.SelectionStart = newCaretPos;
            _translationTextBox.SelectionEnd = newCaretPos;
        }, DispatcherPriority.Background);
    }

    private void OnPageUpButtonClick(object? sender, RoutedEventArgs e)
        => ExecuteShortcutAction(ShortcutAction.NavigatePageUp);

    private void OnPageDownButtonClick(object? sender, RoutedEventArgs e)
        => ExecuteShortcutAction(ShortcutAction.NavigatePageDown);

    /// <summary>
    /// 分组选择改变
    /// </summary>
    private void OnGroupSelectionChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton)
        {
            if (radioButton == Group0RadioButton)
            {
                Edit.SwitchGroupCommand.Execute(0);
            }
            else if (radioButton == Group1RadioButton)
            {
                Edit.SwitchGroupCommand.Execute(1);
            }
            // RadioButton 同步和颜色更新由 OnGroupChanged 统一处理
        }
    }

    /// <summary>
    /// 更新分组按钮的颜色样式
    /// </summary>
    private void UpdateGroupButtonColors()
    {
        if (Group0RadioButton == null || Group1RadioButton == null)
            return;

        try
        {
            var settings = _settingsProvider.Current;

            // Group0 (框内) - 分组索引为1
            var group0ColorHex = settings.Colors.GroupColors.GetValueOrDefault(1, "#E74856");
            var group0Color = Avalonia.Media.Color.Parse(group0ColorHex);
            var group0HoverColor = AdjustBrightness(group0Color, 1.2); // 增加亮度
            var group0PressedColor = AdjustBrightness(group0Color, 0.7); // 减少亮度

            // Group1 (框外) - 分组索引为2
            var group1ColorHex = settings.Colors.GroupColors.GetValueOrDefault(2, "#1E90FF");
            var group1Color = Avalonia.Media.Color.Parse(group1ColorHex);
            var group1HoverColor = AdjustBrightness(group1Color, 1.2);
            var group1PressedColor = AdjustBrightness(group1Color, 0.7);

            // 更新Window Resources中的颜色
            UpdateColorResource("Group0ColorBrush", group0Color);
            UpdateColorResource("Group1ColorBrush", group1Color);
            UpdateColorResource("Group0ColorHoverBrush", group0HoverColor);
            UpdateColorResource("Group1ColorHoverBrush", group1HoverColor);
            UpdateColorResource("Group0ColorPressedBrush", group0PressedColor);
            UpdateColorResource("Group1ColorPressedBrush", group1PressedColor);
        }
        catch
        {
            // 如果出错，使用默认颜色
        }
    }

    /// <summary>
    /// 更新Window资源中的颜色
    /// </summary>
    private void UpdateColorResource(string key, Avalonia.Media.Color color)
    {
        if (Resources.TryGetValue(key, out var existingBrush) && existingBrush is Avalonia.Media.SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    /// <summary>
    /// 调整颜色亮度
    /// </summary>
    private static Avalonia.Media.Color AdjustBrightness(Avalonia.Media.Color color, double factor)
    {
        var r = (byte)Math.Min(255, (int)(color.R * factor));
        var g = (byte)Math.Min(255, (int)(color.G * factor));
        var b = (byte)Math.Min(255, (int)(color.B * factor));
        return Avalonia.Media.Color.FromArgb(color.A, r, g, b);
    }


    /// <summary>
    /// <summary>
    /// 文本框按键处理：Ctrl+Enter 取消文本框聚焦并阻止 Enter 插入换行。
    /// </summary>
    private void OnTranslationTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0 || (modifiers & KeyModifiers.Meta) != 0;
        
        if (!isCtrlPressed || (e.Key != Key.Return && e.Key != Key.Enter))
            return;
        
        _isHandlingCtrlEnter = true;
        try
        {
            var text = _translationTextBox?.Text ?? "";
            if (text.Length > 0 && text[^1] == '\n')
            {
                _translationTextBox!.Text = text[..^1];
                if (_translationTextBox.Text.Length > 0 && _translationTextBox.Text[^1] == '\r')
                    _translationTextBox.Text = _translationTextBox.Text[..^1];
            }
        }
        finally { _isHandlingCtrlEnter = false; }

        e.Handled = true;
        _isIntentionalBlur = true;
        
        Dispatcher.UIThread.Post(() =>
            TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null),
            DispatcherPriority.Loaded);
        
        Dispatcher.UIThread.Post(() => _isIntentionalBlur = false,
            DispatcherPriority.Background);
    }

    /// <summary>
    /// 崩溃恢复：TextBox 逐字变更时启动 200ms 防抖定时器。
    /// </summary>
    private void OnTranslationTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_recoveryDebounce == null) return;
        if (!Edit.IsEditMode) return;
        _recoveryDebounce.Stop();
        _recoveryDebounce.Start();
    }

    /// <summary>
    /// 文本框获得焦点时的拦截器。
    /// 若处于"主动离焦"状态（_isIntentionalBlur），立即清除焦点并复位标志。
    /// 正常情况下（用户点击文本框、AutoFocusTextBox 触发）不受影响。
    /// </summary>
    private void OnTranslationTextBoxGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (!_isIntentionalBlur) return;

        _isIntentionalBlur = false;
        Dispatcher.UIThread.Post(
            () => TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null),
            DispatcherPriority.Input);
    }

}
