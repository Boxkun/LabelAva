namespace LabelAva.Models;

/// <summary>
/// 树视图项：代表一条翻译
/// </summary>
public class TranslationTreeItem
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// 分组索引（1=红色，2=蓝色）
    /// </summary>
    public int GroupIndex { get; set; }
}
