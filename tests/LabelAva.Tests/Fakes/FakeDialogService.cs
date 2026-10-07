using System.Collections.Generic;
using System.Threading.Tasks;
using LabelAva.Models;
using LabelAva.Services;

namespace LabelAva.Tests.Fakes;

/// <summary>
/// IDialogService 的假实现：不出窗口，按预设答案返回，并记录调用次数。
/// 有了它，DocumentViewModel 的保存确认 / 崩溃恢复 / 图片关联分支才可测 ——
/// 这些分支在 headless 下若走真实实现会打开真窗口，测试会被挂住。
/// </summary>
internal sealed class FakeDialogService : IDialogService
{
    public UnsavedChangesResult UnsavedChangesAnswer { get; set; } = UnsavedChangesResult.Cancel;

    public RecoveryResult RecoveryAnswer { get; set; } = RecoveryResult.Discard;

    public ImageSelectionResult? ImageSelectionAnswer { get; set; }

    public ImageAssociationResult? ImageAssociationAnswer { get; set; }

    public int UnsavedChangesCalls { get; private set; }

    public int RecoveryCalls { get; private set; }

    public int ImageSelectionCalls { get; private set; }

    public int ImageAssociationCalls { get; private set; }

    public Task<UnsavedChangesResult> ShowUnsavedChangesAsync(string message)
    {
        UnsavedChangesCalls++;
        return Task.FromResult(UnsavedChangesAnswer);
    }

    public Task<RecoveryResult> ShowRecoveryAsync(string message)
    {
        RecoveryCalls++;
        return Task.FromResult(RecoveryAnswer);
    }

    public Task<ImageSelectionResult?> ShowImageSelectionAsync(List<string> imageFiles, string defaultFileName)
    {
        ImageSelectionCalls++;
        return Task.FromResult(ImageSelectionAnswer);
    }

    public Task<ImageAssociationResult?> ShowImageAssociationAsync(
        List<ImageAssociationItem> items, string imageFolderPath)
    {
        ImageAssociationCalls++;
        return Task.FromResult(ImageAssociationAnswer);
    }
}
