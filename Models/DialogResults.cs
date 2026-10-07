using System.Collections.Generic;

namespace LabelAva.Models;

// 这三个类型原本定义在 DocumentViewModel.cs 里。它们是纯数据（无 VM 依赖），
// 而 IDialogService 位于 Services 层、需要引用它们，所以移到这里以避免 Services → ViewModels 的反向依赖。

/// <summary>未保存更改对话框结果</summary>
public enum UnsavedChangesResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>崩溃恢复对话框结果</summary>
public enum RecoveryResult
{
    Recover,
    Discard
}

/// <summary>图片选择对话框结果</summary>
public class ImageSelectionResult
{
    public List<string> SelectedImagePaths { get; set; } = new();
    public string FileName { get; set; } = string.Empty;
}
