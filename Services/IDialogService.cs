using System.Collections.Generic;
using System.Threading.Tasks;
using LabelAva.Models;

namespace LabelAva.Services;

/// <summary>
/// 需要用户决策的对话框集合。
///
/// 抽成接口有两个目的：
/// 一是把「构造窗口」这件事从 MainWindow 与 ViewModel 里挪进一个可独立审视的类；
/// 二是让 DocumentViewModel 的保存确认、崩溃恢复、图片关联这些分支能在测试里
/// 注入假实现 —— 它们原本会打开真窗口，headless 环境下无法覆盖。
/// </summary>
public interface IDialogService
{
    /// <summary>询问未保存的更改如何处理。</summary>
    Task<UnsavedChangesResult> ShowUnsavedChangesAsync(string message);

    /// <summary>询问是否从崩溃恢复备份加载。</summary>
    Task<RecoveryResult> ShowRecoveryAsync(string message);

    /// <summary>选择要关联的图片。</summary>
    Task<ImageSelectionResult?> ShowImageSelectionAsync(List<string> imageFiles, string defaultFileName);

    /// <summary>打开图片关联管理器。</summary>
    Task<ImageAssociationResult?> ShowImageAssociationAsync(List<ImageAssociationItem> items, string imageFolderPath);
}
