using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using LabelAva.Models;
using LabelAva.Services;
using LabelAva.Tests.Fakes;
using LabelAva.Tests.Fixtures;
using LabelAva.ViewModels;

namespace LabelAva.Tests;

/// <summary>
/// DocumentViewModel 的对话框分支测试。
///
/// 这些分支（保存确认 / 崩溃恢复 / 图片关联）在把对话框抽成 IDialogService 之前
/// 是无法自动覆盖的：真实实现会打开真窗口，headless 跑不动。
/// </summary>
public class DocumentDialogTests
{
    [AvaloniaFact]
    public void 有未保存更改时关闭会先询问_选取消则不关闭()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);
        var (vm, dialogs) = CreateViewModel();

        RunSync(vm.OpenTranslationFileAsync(project.TranslationPath));
        vm.SetDirty(true);
        dialogs.UnsavedChangesAnswer = UnsavedChangesResult.Cancel;

        RunSync(vm.CloseCommand.ExecuteAsync(null));

        Assert.Equal(1, dialogs.UnsavedChangesCalls);
        Assert.True(vm.HasDocument, "选择取消后文档应当仍然打开");
        Assert.True(vm.IsDirty, "选择取消后脏标记应当保留");
    }

    [AvaloniaFact]
    public void 有未保存更改时关闭_选不保存则丢弃并关闭()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);
        var (vm, dialogs) = CreateViewModel();

        RunSync(vm.OpenTranslationFileAsync(project.TranslationPath));
        vm.SetDirty(true);
        dialogs.UnsavedChangesAnswer = UnsavedChangesResult.Discard;

        RunSync(vm.CloseCommand.ExecuteAsync(null));

        Assert.Equal(1, dialogs.UnsavedChangesCalls);
        Assert.False(vm.HasDocument, "选择不保存后文档应当关闭");
        Assert.False(vm.IsDirty);
        Assert.Null(vm.FilePath);
    }

    [AvaloniaFact]
    public void 没有未保存更改时关闭不会询问()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);
        var (vm, dialogs) = CreateViewModel();

        RunSync(vm.OpenTranslationFileAsync(project.TranslationPath));
        Assert.False(vm.IsDirty, "前置条件：刚打开的文档不应是脏的");

        RunSync(vm.CloseCommand.ExecuteAsync(null));

        Assert.Equal(0, dialogs.UnsavedChangesCalls);
        Assert.False(vm.HasDocument);
    }

    [AvaloniaFact]
    public void 图片缺失时打开会走关联管理器_用户取消则放弃加载()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);
        var (vm, dialogs) = CreateViewModel();

        // 删掉翻译文件里引用的一张图，制造 Missing
        File.Delete(Path.Combine(project.Root, "01.png"));
        dialogs.ImageAssociationAnswer = null; // 用户在关联管理器里取消

        RunSync(vm.OpenTranslationFileAsync(project.TranslationPath));

        Assert.Equal(1, dialogs.ImageAssociationCalls);
        Assert.False(vm.HasDocument, "用户取消关联后不应载入文档");
        Assert.Null(vm.TranslationData);
    }

    private static (DocumentViewModel Vm, FakeDialogService Dialogs) CreateViewModel()
    {
        var statusBar = new StatusBarViewModel();
        var history = new HistoryViewModel(new HistoryManager(), statusBar);
        var dialogs = new FakeDialogService();
        var vm = new DocumentViewModel(
            new FakeFileService(), history, statusBar, dialogs, new AppSettingsProvider());
        return (vm, dialogs);
    }

    /// <summary>把异步 VM 操作推到完成（假对话框返回已完成的 Task，通常立即完成）。</summary>
    private static void RunSync(Task task, string description = "异步操作完成")
    {
        HeadlessPump.Until(() => task.IsCompleted, description);
        task.GetAwaiter().GetResult();
    }
}
