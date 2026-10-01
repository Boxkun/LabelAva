using Avalonia.Headless.XUnit;

namespace LabelAva.Tests;

/// <summary>
/// 快捷输入按钮的文本写入行为。
/// 这两条是命名化的回归守卫 —— 行为轨迹基线也能发现回归，
/// 但基线失败只会给出「某一行不一致」，命名测试能直接说清是哪条行为坏了。
/// </summary>
public class QuickInputTests
{
    [AvaloniaFact]
    public void 快捷输入在有选区时替换选中内容()
    {
        using var session = new EditModeSession();

        UiDriver.SelectAllText(session.TextBox);
        var selectedLength = session.TextBox.SelectionEnd - session.TextBox.SelectionStart;
        Assert.True(selectedLength > 0,
            $"前置条件：应当已经选中了文本（text='{session.TextBox.Text}' " +
            $"start={session.TextBox.SelectionStart} end={session.TextBox.SelectionEnd} " +
            $"caret={session.TextBox.CaretIndex}）");

        // 立即点击，中间不泵调度器（否则 POST 的光标修复会把选区收拢到末尾）
        UiDriver.Click(session.DligQuickInputButton);

        Assert.Equal("~~", session.TextBox.Text);
        Assert.Equal(2, session.TextBox.CaretIndex);
        Assert.Equal(session.TextBox.CaretIndex, session.TextBox.SelectionStart);
        Assert.Equal(session.TextBox.CaretIndex, session.TextBox.SelectionEnd);
    }

    [AvaloniaFact]
    public void 快捷输入在无选区时插入到光标处()
    {
        using var session = new EditModeSession();

        // 显式把光标放到文本中间（造出确定的无选区状态）
        var original = session.TextBox.Text ?? string.Empty;
        var middle = original.Length / 2;
        UiDriver.CollapseCaretAt(session.TextBox, middle);

        UiDriver.Click(session.DligQuickInputButton);

        Assert.Equal(original.Insert(middle, "~~"), session.TextBox.Text);
        Assert.Equal(middle + 2, session.TextBox.CaretIndex);
    }
}
