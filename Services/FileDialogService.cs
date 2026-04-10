using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LabelAva.Services;

/// <summary>
/// 基于 Avalonia StorageProvider 的文件操作实现
/// </summary>
public class FileDialogService : IFileService
{
    private readonly Func<TopLevel?> _getTopLevel;

    public FileDialogService(Func<TopLevel?> getTopLevel)
    {
        _getTopLevel = getTopLevel;
    }

    public async Task<string?> PickOpenFileAsync(string title, FilePickerFileType[]? filters = null)
    {
        var topLevel = _getTopLevel();
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters ?? Array.Empty<FilePickerFileType>()
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> PickSaveFileAsync(string title, string defaultExtension, FilePickerFileType[]? filters = null)
    {
        var topLevel = _getTopLevel();
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            ShowOverwritePrompt = true,
            FileTypeChoices = filters ?? Array.Empty<FilePickerFileType>()
        });

        return file != null ? file.Path.LocalPath : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = _getTopLevel();
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
