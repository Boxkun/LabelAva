using System.Collections.Generic;

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
}
