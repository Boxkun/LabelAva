using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabelAva.Models;

public enum ImageValidationStatus
{
    OK,
    Missing,
    /// <summary>文件存在但魔数签名与扩展名不符</summary>
    FormatMismatch,
}

public class ImageAssociationItem : INotifyPropertyChanged
{
    private ImageValidationStatus _status;
    private string _statusText = string.Empty;
    private string? _newPath;

    public string ImageName { get; set; } = string.Empty;

    public ImageValidationStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusForeground));
            }
        }
    }

    public string StatusForeground => _status switch
    {
        ImageValidationStatus.Missing => "#F44336",
        ImageValidationStatus.FormatMismatch => "#FF9800",
        _ => "#888888"
    };

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string? NewPath
    {
        get => _newPath;
        set
        {
            if (_newPath != value)
            {
                _newPath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>该图片对应的标注数量（供删除确认弹窗展示）</summary>
    public int LabelCount { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ImageAssociationResult
{
    public string FolderPath { get; set; } = string.Empty;
    public bool WriteToFile { get; set; }
    public Dictionary<string, string> Remappings { get; set; } = new();

    /// <summary>排序后的最终图片顺序（用于重排持久化）</summary>
    public List<string>? OrderedImageNames { get; set; }

    /// <summary>新增图片：图片名 → 文件路径</summary>
    public Dictionary<string, string>? AddedImages { get; set; }

    /// <summary>待删除的图片名列表（级联删除其 LabelItem）</summary>
    public List<string>? RemovedImageNames { get; set; }
}
