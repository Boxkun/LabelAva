using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabelAva.Models;

/// <summary>
/// 树视图项：代表一张图片及其翻译
/// </summary>
public class ImageTreeItem : INotifyPropertyChanged
{
    public string ImageName { get; set; } = string.Empty;
    public List<TranslationTreeItem> Translations { get; set; } = new();

    // 添加展开状态属性
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    // 保存加载时的 fit 缩放比例
    public double FitScale { get; set; } = 1.0;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
