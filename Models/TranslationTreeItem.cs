using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabelAva.Models;

/// <summary>
/// 树视图项：代表一条翻译
/// </summary>
public class TranslationTreeItem : INotifyPropertyChanged
{
    private int _index;
    private string _text = string.Empty;
    private int _groupIndex;

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
    
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
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
