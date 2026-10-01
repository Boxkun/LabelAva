using System;
using Avalonia.Controls;
using LabelAva.Tests.Fixtures;

namespace LabelAva.Tests;

/// <summary>
/// 「已打开夹具项目、已选中标记 1、已进入编辑模式」的一次性会话，
/// 供需要操作编辑区的测试复用。释放时连同隔离的 AppData 一起清理。
/// </summary>
internal sealed class EditModeSession : IDisposable
{
    private readonly TempAppData _appData = new();

    public EditModeSession()
    {
        Project = new ProjectFixture(_appData);
        Harness = new MainWindowHarness(_appData);

        var openTask = Harness.Vm.Document.OpenTranslationFileAsync(Project.TranslationPath);
        HeadlessPump.Until(() => openTask.IsCompleted, "打开翻译文件完成");
        openTask.GetAwaiter().GetResult();

        // 打开文档之后，导航树是连锁构建出来的；不等它落地，SelectLabelByIndex 会找不到树项
        HeadlessPump.Until(() => Harness.Vm.Navigation.TreeItems.Count > 0, "导航树构建完成");
        HeadlessPump.Drain();

        Harness.Vm.Navigation.SelectLabelByIndex(1);
        HeadlessPump.Until(() => Harness.Vm.Navigation.SelectedTranslationItem is not null, "选中标记 1");
        // 选中之后必须先泵一轮：切编辑模式与文本框绑定都依赖上一步的连锁更新落地
        HeadlessPump.Drain();

        if (!Harness.Vm.Edit.ToggleEditModeCommand.CanExecute(null))
            throw new InvalidOperationException("前置条件不满足：当前状态不允许切换到编辑模式");

        Harness.Vm.Edit.ToggleEditModeCommand.Execute(null);
        HeadlessPump.Until(() => Harness.Vm.Edit.IsEditMode, "进入编辑模式");

        TextBox = Harness.Window.FindControl<TextBox>("TranslationTextBox")
            ?? throw new InvalidOperationException("找不到 TranslationTextBox");

        // 文本框通过绑定拿到选中标记的文本；拿不到说明夹具或时序有问题，直接报错而不是静默降级
        HeadlessPump.Until(() => !string.IsNullOrEmpty(TextBox.Text), "文本框已绑定选中标记的文本");

        // 快捷输入工具栏的按钮要等一次布局 pass 才会实例化
        HeadlessPump.Until(() => UiDriver.HasDligQuickInputButton(Harness.Window), "快捷输入按钮完成布局");
    }

    public ProjectFixture Project { get; }

    public MainWindowHarness Harness { get; }

    public TextBox TextBox { get; }

    public Button DligQuickInputButton => UiDriver.FindDligQuickInputButton(Harness.Window);

    public void Dispose()
    {
        Harness.Dispose();
        _appData.Dispose();
    }
}
