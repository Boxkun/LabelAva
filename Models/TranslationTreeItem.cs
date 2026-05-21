using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabelAva.Models;

/// <summary>
/// 树视图项：代表一条翻译。Text 属性透传 LabelItem.Text。
/// </summary>
public class TranslationTreeItem : INotifyPropertyChanged
{
    private int _index;
    private int _groupIndex;

    public bool IsExpanded => false;

    /// <summary>对应的数据层标签对象。Text 属性透传此对象的 Text。</summary>
    public LabelItem? LabelItem { get; set; }

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>文本内容 — 透传 LabelItem.Text。写入即时到数据层。</summary>
    public string Text
    {
        get => LabelItem?.Text ?? string.Empty;
        set
        {
            if (LabelItem == null) return;
            if (LabelItem.Text != value)
            {
                LabelItem.Text = value;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// 分组索引（1=红色，2=蓝色）
    /// </summary>
    public int GroupIndex
    {
        get => _groupIndex;
        set
        {
            if (_groupIndex != value)
            {
                _groupIndex = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
