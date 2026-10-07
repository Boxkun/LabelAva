using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using LabelAva.Commands;
using LabelAva.Models;

namespace LabelAva.Tests;

/// <summary>
/// 树重建前后「选中项」这个游标的保持与迁移规则。
///
/// 背景：`Navigation.BuildTreeView` 是 `TreeItems.Clear()` + 全部新建实例 —— 被选中的那个
/// 视图实例重建后就不存在了，而 TreeView 会因此报出 null 选中，`OnTreeViewSelectionChanged`
/// 又会把这个 null 无条件回写进 ViewModel。于是「重建后恢复选中」与「null 回写」在赛跑，
/// 谁赢取决于布局时序（CI 上三个平台随机复现，表现为翻译文本框突然变空、被禁用）。
///
/// 这里把规则钉成确定的断言：
///  - 同一个标记（内容变更/分组/重排）→ 按**对象引用**选回来（重排会重编号索引，按索引会选错标记）；
///  - 标记被删除 → 选「原索引位置的邻居」，没有则退一格，全被删空则落到图片节点；
///  - 关闭文档 → 仍然清空（`ClearNavigation` 在 VM 侧直接清空，不依赖 TreeView 的 null）。
/// </summary>
public class TreeSelectionTests
{
    [AvaloniaFact]
    public void 删除选中的中间标记后应选中原索引位置的邻居()
    {
        using var session = new EditModeSession();
        var vm = session.Harness.Vm;
        var labels = vm.Document.TranslationData!.ImageLabels["01.png"];
        var neighborText = labels.First(l => l.TextIndex == 3).Text;

        vm.Navigation.SelectLabelByIndex(2);
        HeadlessPump.Drain();

        vm.History.ExecuteCommand(new DeleteLabelCommand(labels, labels.First(l => l.TextIndex == 2)));
        HeadlessPump.Drain();

        // 剩下的标记被重编号，原索引 2 现在指向原来的第 3 个标记
        Assert.NotNull(vm.Navigation.SelectedTranslationItem);
        Assert.Equal(neighborText, vm.Navigation.SelectedTranslationItem!.LabelItem.Text);
    }

    [AvaloniaFact]
    public void 删除选中的最后一个标记后应退回前一个标记()
    {
        using var session = new EditModeSession();
        var vm = session.Harness.Vm;
        var labels = vm.Document.TranslationData!.ImageLabels["01.png"];
        var previousText = labels.First(l => l.TextIndex == 2).Text;

        vm.Navigation.SelectLabelByIndex(3);
        HeadlessPump.Drain();

        vm.History.ExecuteCommand(new DeleteLabelCommand(labels, labels.First(l => l.TextIndex == 3)));
        HeadlessPump.Drain();

        // 原索引 3 已不存在 → 退一格选中现在的最后一个（原第 2 个）
        Assert.NotNull(vm.Navigation.SelectedTranslationItem);
        Assert.Equal(previousText, vm.Navigation.SelectedTranslationItem!.LabelItem.Text);
    }

    [AvaloniaFact]
    public void 删空所有标记后应选中图片节点()
    {
        using var session = new EditModeSession();
        var vm = session.Harness.Vm;
        var labels = vm.Document.TranslationData!.ImageLabels["01.png"];

        while (labels.Count > 0)
        {
            vm.History.ExecuteCommand(new DeleteLabelCommand(labels, labels[0]));
            HeadlessPump.Drain();
        }

        // 标记全没了：落到图片节点，翻译文本框自然没有绑定目标
        Assert.IsType<ImageTreeItem>(vm.Navigation.SelectedItem);
        Assert.Null(vm.Navigation.SelectedTranslationItem);
    }

    [AvaloniaFact]
    public void 拖拽重排后应仍选中同一个标记()
    {
        using var session = new EditModeSession();
        var window = session.Harness.Window;
        var vm = session.Harness.Vm;
        var before = vm.Navigation.SelectedTranslationItem!.LabelItem;

        var treeView = window.FindControl<TreeView>("ImageTreeView")!;
        vm.Navigation.TreeItems[0].IsExpanded = true;
        HeadlessPump.Until(
            () => UiDriver.TranslationItemBoxes(window, treeView).Count >= 3,
            "翻译节点容器完成布局");

        var boxes = UiDriver.TranslationItemBoxes(window, treeView);
        var source = boxes[0];   // 当前选中的就是标记 1
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

        // 重排后标记 1 的索引由 1 变成 2：恢复若按索引匹配，就会选中「索引 1 现在指向的那个标记」。
        // 应用改成了按对象引用恢复，所以这里必须仍是同一个标记。
        //（注：在这台机器上旧实现也偶然满足这条 —— 该场景没走到恢复分支 —— 所以它是护栏而非判别用例，
        //  真正判别旧实现错误的是「删除最后一个标记」那条。）
        Assert.NotNull(vm.Navigation.SelectedTranslationItem);
        Assert.Same(before, vm.Navigation.SelectedTranslationItem!.LabelItem);
    }

    /// <summary>
    /// 关闭文档时选中项必须清空，否则状态会跨文档残留。
    /// 这条同时是「忽略重建导致的 null 回写」的护栏：那个改动不能挡住这条路径。
    /// </summary>
    [AvaloniaFact]
    public void 关闭文档后应当清空选中项()
    {
        using var session = new EditModeSession();
        var vm = session.Harness.Vm;

        Assert.NotNull(vm.Navigation.SelectedTranslationItem);

        vm.Document.ForceCloseDocument();
        HeadlessPump.Drain();

        Assert.Null(vm.Navigation.SelectedTranslationItem);
        Assert.False(vm.Navigation.HasDocument);
    }
}
