using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace LabelAva.Tests;

/// <summary>
/// 树视图拖拽重排。
///
/// 这是行为轨迹基线覆盖不到的一块（基线只走 VM 命令与键盘），所以单独用**真实指针事件**
/// 驱动：MouseDown → 超过阈值 → MouseMove 到落点 → MouseUp。
/// 抽 TreeDragDropController 之前必须先有这条守卫。
/// </summary>
public class TreeDragReorderTests
{
    [AvaloniaFact]
    public void 拖拽标记到另一标记之前会重排_且可撤销还原()
    {
        using var session = new EditModeSession();
        var window = session.Harness.Window;
        var vm = session.Harness.Vm;

        var treeView = window.FindControl<TreeView>("ImageTreeView")
            ?? throw new System.InvalidOperationException("找不到 ImageTreeView");

        // 打开文档后第一个图片节点默认展开；等三个翻译节点的容器真实布局出来
        vm.Navigation.TreeItems[0].IsExpanded = true;
        HeadlessPump.Until(
            () => UiDriver.TranslationItemBoxes(window, treeView).Count >= 3,
            "翻译节点容器完成布局");

        var boxes = UiDriver.TranslationItemBoxes(window, treeView);
        var source = boxes[0];   // 标记 1（X=0.25）
        var target = boxes[2];   // 标记 3（X=0.75）

        // 落点取目标容器中点以上：中点判定会返回「插到它之前」
        var dropPoint = new Point(target.Center.X, target.Center.Y - target.Container.Bounds.Height / 4);

        window.MouseDown(source.Center, MouseButton.Left, RawInputModifiers.None);
        HeadlessPump.Drain();

        // 先移动超过 4px 阈值，让状态机进入 DRAGGING
        window.MouseMove(new Point(source.Center.X, source.Center.Y + 40), RawInputModifiers.LeftMouseButton);
        HeadlessPump.Drain();

        window.MouseMove(dropPoint, RawInputModifiers.LeftMouseButton);
        HeadlessPump.Drain();

        // 拖拽中的视觉副作用（这些不在行为轨迹里，只能在这里守）
        var preview = window.FindControl<Border>("TreeDragPreview")!;
        var dropLine = window.FindControl<Rectangle>("TreeDropLine")!;
        Assert.True(preview.IsVisible, "拖拽中应当显示拖拽预览");
        Assert.True(dropLine.IsVisible, "找到落点后应当显示插入线");
        Assert.NotNull(window.Cursor);

        window.MouseUp(dropPoint, MouseButton.Left, RawInputModifiers.None);
        HeadlessPump.Drain();

        Assert.False(preview.IsVisible, "松手后应当清掉拖拽预览");
        Assert.False(dropLine.IsVisible, "松手后应当清掉插入线");
        Assert.Null(window.Cursor);

        var labels = vm.Document.TranslationData!.ImageLabels["01.png"];

        // 拖拽前顺序 X = 0.25 / 0.50 / 0.75；把第一个拖到第三个之前 → 0.50 / 0.25 / 0.75
        Assert.Equal(new[] { 0.50, 0.25, 0.75 }, labels.Select(l => l.X).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, labels.Select(l => l.TextIndex).ToArray());

        Assert.True(vm.History.CanUndo, "重排应当进入历史栈");
        Assert.Contains("重新排序标注", vm.History.UndoHeader);

        vm.History.UndoCommand.Execute(null);
        HeadlessPump.Drain();

        var restored = vm.Document.TranslationData!.ImageLabels["01.png"];
        Assert.Equal(new[] { 0.25, 0.50, 0.75 }, restored.Select(l => l.X).ToArray());
    }

    [AvaloniaFact]
    public void 查看模式下拖拽不会重排()
    {
        using var session = new EditModeSession();
        var window = session.Harness.Window;
        var vm = session.Harness.Vm;

        // 退出编辑模式：阈值检测处应当被 RequireEditMode 拦下
        vm.Edit.ToggleEditModeCommand.Execute(null);
        HeadlessPump.Drain();
        Assert.False(vm.Edit.IsEditMode, "前置条件：应处于查看模式");

        var treeView = window.FindControl<TreeView>("ImageTreeView")!;
        vm.Navigation.TreeItems[0].IsExpanded = true;
        HeadlessPump.Until(
            () => UiDriver.TranslationItemBoxes(window, treeView).Count >= 3,
            "翻译节点容器完成布局");

        var boxes = UiDriver.TranslationItemBoxes(window, treeView);
        var source = boxes[0];
        var target = boxes[2];
        var dropPoint = new Point(target.Center.X, target.Center.Y - target.Container.Bounds.Height / 4);

        window.MouseDown(source.Center, MouseButton.Left, RawInputModifiers.None);
        HeadlessPump.Drain();
        window.MouseMove(new Point(source.Center.X, source.Center.Y + 40), RawInputModifiers.LeftMouseButton);
        HeadlessPump.Drain();
        window.MouseMove(dropPoint, RawInputModifiers.LeftMouseButton);
        HeadlessPump.Drain();
        window.MouseUp(dropPoint, MouseButton.Left, RawInputModifiers.None);
        HeadlessPump.Drain();

        var labels = vm.Document.TranslationData!.ImageLabels["01.png"];
        Assert.Equal(new[] { 0.25, 0.50, 0.75 }, labels.Select(l => l.X).ToArray());
        Assert.False(vm.History.CanUndo, "查看模式下不应产生历史记录");
    }
}
