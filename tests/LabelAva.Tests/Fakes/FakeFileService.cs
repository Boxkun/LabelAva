using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using LabelAva.Services;

namespace LabelAva.Tests.Fakes;

/// <summary>
/// IFileService 的假实现：所有选择对话框都返回预设值（默认 null = 用户取消）。
/// 给定了路径的 OpenTranslationFileAsync 不会走到这里，仅在需要时使用。
/// </summary>
internal sealed class FakeFileService : IFileService
{
    public string? OpenFileResult { get; set; }

    public string? SaveFileResult { get; set; }

    public string? FolderResult { get; set; }

    public int PickOpenFileCalls { get; private set; }

    public Task<string?> PickOpenFileAsync(string title, FilePickerFileType[]? filters = null)
    {
        PickOpenFileCalls++;
        return Task.FromResult(OpenFileResult);
    }

    public Task<string?> PickSaveFileAsync(
        string title, string defaultExtension, FilePickerFileType[]? filters = null)
        => Task.FromResult(SaveFileResult);

    public Task<string?> PickFolderAsync(string title)
        => Task.FromResult(FolderResult);
}
