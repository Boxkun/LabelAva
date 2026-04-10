using Avalonia.Platform.Storage;

namespace LabelAva.Services;

/// <summary>
/// 文件操作服务抽象，解耦 ViewModel 对 StorageProvider 的直接依赖
/// </summary>
public interface IFileService
{
    /// <summary>打开文件选择对话框</summary>
    /// <returns>选中的文件路径，用户取消返回 null</returns>
    Task<string?> PickOpenFileAsync(string title, FilePickerFileType[]? filters = null);

    /// <summary>打开保存文件对话框</summary>
    /// <returns>保存路径，用户取消返回 null</returns>
    Task<string?> PickSaveFileAsync(string title, string defaultExtension, FilePickerFileType[]? filters = null);

    /// <summary>打开文件夹选择对话框</summary>
    /// <returns>选中的文件夹路径，用户取消返回 null</returns>
    Task<string?> PickFolderAsync(string title);
}
