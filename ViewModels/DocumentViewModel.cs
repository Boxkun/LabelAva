using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelAva.Models;
using LabelAva.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LabelAva.ViewModels;

/// <summary>未保存更改对话框结果</summary>
public enum UnsavedChangesResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>图片选择对话框结果</summary>
public class ImageSelectionResult
{
    public List<string> SelectedImagePaths { get; set; } = new();
    public string FileName { get; set; } = string.Empty;
}

/// <summary>文档打开事件参数</summary>
public class DocumentOpenedEventArgs : EventArgs
{
    public TranslationData TranslationData { get; set; } = null!;
    public string ImageFolderPath { get; set; } = string.Empty;
    public List<string> ImageNames { get; set; } = new();
}

public partial class DocumentViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly HistoryViewModel _history;
    private readonly StatusBarViewModel _statusBar;
    private readonly TranslationParser _parser = new();

    // 回调：自定义对话框（UI 层注入）
    private readonly Func<string, Task<UnsavedChangesResult>> _showUnsavedChangesDialog;
    private readonly Func<List<string>, string, Task<ImageSelectionResult?>> _showImageSelectionDialog;

    // 自动保存定时器
    private DispatcherTimer? _autoSaveTimer;

    // ========================
    // 状态属性
    // ========================

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private bool _hasDocument;

    [ObservableProperty]
    private TranslationData? _translationData;

    [ObservableProperty]
    private string? _imageFolderPath;

    // ========================
    // 派生属性
    // ========================

    /// <summary>窗口标题</summary>
    public string WindowTitle
    {
        get
        {
            var title = "LabelAva";
            if (!string.IsNullOrEmpty(FilePath))
            {
                var fileName = Path.GetFileName(FilePath);
                title = $"LabelAva - {fileName}";
            }
            title += (IsDirty ? " *" : "");
            return title;
        }
    }

    /// <summary>当前文件名（不含路径）</summary>
    public string FileName => string.IsNullOrEmpty(FilePath)
        ? string.Empty
        : Path.GetFileName(FilePath);

    /// <summary>保存命令是否可用</summary>
    public bool CanSave => HasDocument && TranslationData != null && !string.IsNullOrEmpty(FilePath);

    /// <summary>另存为命令是否可用</summary>
    public bool CanSaveAs => HasDocument && TranslationData != null;

    /// <summary>关闭翻译命令是否可用</summary>
    public bool CanCloseTranslation => HasDocument;

    // ========================
    // 命令
    // ========================

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        await SaveInternalAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSaveAs))]
    private async Task SaveAs()
    {
        await SaveAsInternalAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCloseTranslation))]
    private async Task Close()
    {
        if (!await ConfirmAndSaveAsync()) return;
        CloseDocumentInternal();
    }

    [RelayCommand]
    private async Task New()
    {
        // 如果已有文档，先确认保存
        if (HasDocument && !await ConfirmAndSaveAsync()) return;

        await CreateNewTranslationAsync();
    }

    [RelayCommand]
    private async Task Open()
    {
        // 如果已有文档，先确认保存
        if (HasDocument && !await ConfirmAndSaveAsync()) return;

        await OpenTranslationFileAsync();
    }

    // ========================
    // 公开方法
    // ========================

    /// <summary>
    /// 检查未保存更改并按需保存。返回 true 表示可以继续关闭/替换。
    /// </summary>
    public async Task<bool> ConfirmAndSaveAsync()
    {
        if (!IsDirty || TranslationData == null) return true;

        var result = await _showUnsavedChangesDialog("工程有尚未保存的更改。是否保存？");

        if (result == UnsavedChangesResult.Save)
        {
            var saved = await SaveOrSaveAsAsync();
            return saved; // 保存失败则不继续
        }
        else if (result == UnsavedChangesResult.Discard)
        {
            return true;
        }
        else // Cancel
        {
            return false;
        }
    }

    /// <summary>设置脏标记</summary>
    public void SetDirty(bool isDirty)
    {
        IsDirty = isDirty;
    }

    /// <summary>
    /// 强制关闭文档（跳过确认），用于窗口关闭场景
    /// </summary>
    public void ForceCloseDocument()
    {
        CloseDocumentInternal();
    }

    // ========================
    // 内部方法
    // ========================

    /// <summary>保存到当前路径（无路径则走另存为）</summary>
    private async Task<bool> SaveOrSaveAsAsync()
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            return await SaveInternalAsync();
        }
        else
        {
            return await SaveAsInternalAsync();
        }
    }

    private async Task<bool> SaveInternalAsync()
    {
        if (TranslationData == null || string.IsNullOrEmpty(FilePath)) return false;

        try
        {
            _parser.Save(FilePath, TranslationData);
            IsDirty = false;
            _statusBar.UpdateStatus($"已保存至: {FileName}", StatusBarViewModel.StatusType.Success);
            return true;
        }
        catch (Exception ex)
        {
            _statusBar.UpdateStatus($"保存失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
            return false;
        }
    }

    private async Task<bool> SaveAsInternalAsync()
    {
        if (TranslationData == null) return false;

        var textFileFilter = new[]
        {
            new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
            new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
        };

        var newPath = await _fileService.PickSaveFileAsync("保存翻译文件", "txt", textFileFilter);
        if (newPath == null) return false; // 用户取消

        try
        {
            _parser.Save(newPath, TranslationData);
            FilePath = newPath;
            IsDirty = false;
            _statusBar.UpdateStatus($"已保存至: {Path.GetFileName(newPath)}", StatusBarViewModel.StatusType.Success);
            return true;
        }
        catch (Exception ex)
        {
            _statusBar.UpdateStatus($"保存失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
            return false;
        }
    }

    private async Task CreateNewTranslationAsync()
    {
        // 1. 选择图片文件夹
        var folderPath = await _fileService.PickFolderAsync("选择图片文件夹");
        if (folderPath == null) return;

        // 2. 扫描图片
        var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        var imageFiles = Directory.GetFiles(folderPath)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0)
        {
            _statusBar.UpdateStatus("所选文件夹中未找到图片文件", StatusBarViewModel.StatusType.Warn);
            return;
        }

        // 3. 弹出图片选择对话框
        var folderName = new DirectoryInfo(folderPath).Name;
        var selectionResult = await _showImageSelectionDialog(imageFiles, folderName);

        if (selectionResult == null || selectionResult.SelectedImagePaths.Count == 0)
            return;

        // 4. 生成翻译文件
        var selectedImages = selectionResult.SelectedImagePaths;
        var userFileName = selectionResult.FileName;
        var content = GenerateTranslationFileContent(selectedImages);

        var translationFileName = $"{userFileName}.txt";
        var translationFilePath = Path.Combine(folderPath, translationFileName);

        // 处理文件名冲突
        var counter = 1;
        while (File.Exists(translationFilePath))
        {
            translationFileName = $"{folderName}_translation_{counter}.txt";
            translationFilePath = Path.Combine(folderPath, translationFileName);
            counter++;
        }

        // 写入文件
        await File.WriteAllTextAsync(translationFilePath, content, System.Text.Encoding.UTF8);

        // 5. 加载翻译数据
        ImageFolderPath = folderPath;
        TranslationData = _parser.Parse(translationFilePath);
        FilePath = translationFilePath;
        IsDirty = false;
        HasDocument = true;

        // 6. 启动自动保存
        StartAutoSaveTimer();

        // 7. 通知 UI 层
        var imageNames = new List<string>(TranslationData.ImageLabels.Keys);
        DocumentOpened?.Invoke(this, new DocumentOpenedEventArgs
        {
            TranslationData = TranslationData,
            ImageFolderPath = ImageFolderPath!,
            ImageNames = imageNames
        });

        _statusBar.UpdateStatus($"已创建翻译文件，包含 {selectedImages.Count} 张图片", StatusBarViewModel.StatusType.Success);
    }

    private async Task OpenTranslationFileAsync()
    {
        var textFileFilter = new[]
        {
            new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
            new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
        };

        var filePath = await _fileService.PickOpenFileAsync("选择翻译文件", textFileFilter);
        if (filePath == null) return;

        try
        {
            TranslationData = _parser.Parse(filePath);
        }
        catch (Exception ex)
        {
            _statusBar.UpdateStatus($"解析翻译文件失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
            return;
        }

        FilePath = filePath;
        ImageFolderPath = Path.GetDirectoryName(filePath);
        IsDirty = false;
        HasDocument = true;

        StartAutoSaveTimer();

        var imageNames = new List<string>(TranslationData.ImageLabels.Keys);
        if (imageNames.Count > 0)
        {
            DocumentOpened?.Invoke(this, new DocumentOpenedEventArgs
            {
                TranslationData = TranslationData,
                ImageFolderPath = ImageFolderPath!,
                ImageNames = imageNames
            });

            _statusBar.UpdateStatus($"已加载 {imageNames.Count} 张图片", StatusBarViewModel.StatusType.Success);
        }
        else
        {
            _statusBar.UpdateStatus("解析翻译文件失败", StatusBarViewModel.StatusType.Error);
        }
    }

    private void CloseDocumentInternal()
    {
        StopAutoSaveTimer();

        TranslationData = null;
        FilePath = null;
        ImageFolderPath = null;
        IsDirty = false;
        HasDocument = false;

        _history.Clear();

        DocumentClosed?.Invoke(this, EventArgs.Empty);
        _statusBar.UpdateStatus("就绪");
    }

    /// <summary>生成翻译文件模板内容</summary>
    private string GenerateTranslationFileContent(List<string> imagePaths)
    {
        var lines = new List<string>();

        lines.Add("1,0");
        lines.Add("-");
        lines.Add("框内");
        lines.Add("框外");
        lines.Add("-");
        lines.Add("LabelAva 1.0");
        lines.Add("");

        foreach (var imagePath in imagePaths)
        {
            var imageName = Path.GetFileName(imagePath);
            lines.Add("");
            lines.Add($">>>>>>>>[{imageName}]<<<<<<<<");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    // ========================
    // 自动保存
    // ========================

    private void StartAutoSaveTimer()
    {
        StopAutoSaveTimer();
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(3)
        };
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
        _autoSaveTimer.Start();
    }

    private void StopAutoSaveTimer()
    {
        if (_autoSaveTimer != null)
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Tick -= OnAutoSaveTimerTick;
            _autoSaveTimer = null;
        }
    }

    private void OnAutoSaveTimerTick(object? sender, EventArgs e)
    {
        if (IsDirty && !string.IsNullOrEmpty(FilePath) && TranslationData != null)
        {
            try
            {
                _parser.Save(FilePath, TranslationData);
                IsDirty = false;
                _statusBar.UpdateStatus("自动保存成功", StatusBarViewModel.StatusType.Success);
            }
            catch
            {
                // 自动保存失败，静默处理
            }
        }
    }

    // ========================
    // 属性变更通知
    // ========================

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    partial void OnFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveAs));
        OnPropertyChanged(nameof(CanCloseTranslation));
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnTranslationDataChanged(TranslationData? value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveAs));
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    // ========================
    // 事件
    // ========================

    /// <summary>文档打开事件（通知 MainWindow 加载图片、构建树视图、切换 UI）</summary>
    public event EventHandler<DocumentOpenedEventArgs>? DocumentOpened;

    /// <summary>文档关闭事件（通知 MainWindow 清理 UI、切换到欢迎屏幕）</summary>
    public event EventHandler? DocumentClosed;

    // ========================
    // 构造函数
    // ========================

    public DocumentViewModel(
        IFileService fileService,
        HistoryViewModel history,
        StatusBarViewModel statusBar,
        Func<string, Task<UnsavedChangesResult>> showUnsavedChangesDialog,
        Func<List<string>, string, Task<ImageSelectionResult?>> showImageSelectionDialog)
    {
        _fileService = fileService;
        _history = history;
        _statusBar = statusBar;
        _showUnsavedChangesDialog = showUnsavedChangesDialog;
        _showImageSelectionDialog = showImageSelectionDialog;
    }
}
