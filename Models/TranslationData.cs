using System.Collections.Generic;
using System.Linq;

namespace LabelAva.Models;

/// <summary>
/// 分组信息
/// </summary>
public class GroupInfo
{
    /// <summary>
    /// 分组索引（从1开始）
    /// </summary>
    public int Index { get; set; }
    
    /// <summary>
    /// 分组名称（如"框内"、"框外"）
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 单个标注数据
/// </summary>
public class LabelItem
{
    /// <summary>
    /// 图片文件名
    /// </summary>
    public string ImageName { get; set; } = string.Empty;
    
    /// <summary>
    /// 当前图片中文本数据编号
    /// </summary>
    public int TextIndex { get; set; }
    
    /// <summary>
    /// 横坐标（相对于图片原分辨率）
    /// </summary>
    public double X { get; set; }
    
    /// <summary>
    /// 纵坐标（相对于图片原分辨率）
    /// </summary>
    public double Y { get; set; }
    
    /// <summary>
    /// 分组索引值（对应GroupInfo的Index）
    /// </summary>
    public int GroupIndex { get; set; }
    
    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// 创建当前对象的深拷贝
    /// </summary>
    public LabelItem Clone()
    {
        return new LabelItem
        {
            ImageName = ImageName,
            TextIndex = TextIndex,
            X = X,
            Y = Y,
            GroupIndex = GroupIndex,
            Text = Text
        };
    }
}

/// <summary>
/// 完整的翻译数据文件解析结果
/// </summary>
public class TranslationData
{
    /// <summary>
    /// 未知参数（第1行）
    /// </summary>
    public string UnknownParam { get; set; } = string.Empty;
    
    /// <summary>
    /// 分组列表
    /// </summary>
    public List<GroupInfo> Groups { get; set; } = new();
    
    /// <summary>
    /// 注释列表
    /// </summary>
    public List<string> Comments { get; set; } = new();
    
    /// <summary>
    /// 所有标注数据（按图片分组）
    /// </summary>
    public Dictionary<string, List<LabelItem>> ImageLabels { get; set; } = new();
    
    /// <summary>
    /// 创建当前对象的深拷贝
    /// </summary>
    public TranslationData Clone()
    {
        var clone = new TranslationData
        {
            UnknownParam = UnknownParam,
            Groups = Groups.Select(g => new GroupInfo { Index = g.Index, Name = g.Name }).ToList(),
            Comments = new List<string>(Comments),
            ImageLabels = new Dictionary<string, List<LabelItem>>()
        };
        
        // 深拷贝每个图片的标签列表
        foreach (var kvp in ImageLabels)
        {
            clone.ImageLabels[kvp.Key] = kvp.Value.Select(l => l.Clone()).ToList();
        }
        
        return clone;
    }
    /// <summary>
    /// 深度比较两个翻译数据是否完全相同（用于拦截空历史记录）
    /// </summary>
    public bool IsEqualTo(TranslationData? other)
    {
        if (other == null) return false;
        if (UnknownParam != other.UnknownParam) return false;
        if (Comments.Count != other.Comments.Count || !Comments.SequenceEqual(other.Comments)) return false;
        if (Groups.Count != other.Groups.Count) return false;
        
        for (int i = 0; i < Groups.Count; i++)
        {
            if (Groups[i].Index != other.Groups[i].Index || Groups[i].Name != other.Groups[i].Name) return false;
        }
        
        if (ImageLabels.Count != other.ImageLabels.Count) return false;
        
        foreach (var kvp in ImageLabels)
        {
            if (!other.ImageLabels.ContainsKey(kvp.Key)) return false;
            var list1 = kvp.Value;
            var list2 = other.ImageLabels[kvp.Key];
            
            if (list1.Count != list2.Count) return false;
            
            for (int i = 0; i < list1.Count; i++)
            {
                var l1 = list1[i];
                var l2 = list2[i];
                // 允许浮点数存在极小误差
                if (l1.TextIndex != l2.TextIndex || l1.GroupIndex != l2.GroupIndex || l1.Text != l2.Text ||
                    Math.Abs(l1.X - l2.X) > 0.0001 || Math.Abs(l1.Y - l2.Y) > 0.0001) 
                    return false;
            }
        }
        return true;
    }
}
