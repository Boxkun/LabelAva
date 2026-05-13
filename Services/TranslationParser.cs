using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using LabelAva.Models;

namespace LabelAva.Services;

/// <summary>
/// 翻译数据文件解析器
/// </summary>
public class TranslationParser
{
    // 匹配图片文件名行: >>>>>>>>[01.jpeg]<<<<<<<<
    private static readonly Regex ImagePattern = new(@"^>>>>>>>>\[(.+)\]<<<<<<<<$");
    
    // 匹配标注数据行: ----------------[1]----------------[0.252,0.077,1]
    private static readonly Regex LabelPattern = new(@"^----------------\[(\d+)\]----------------\[([0-9.]+),([0-9.]+),(\d+)\]$");
    
    // 匹配分组标记行: -
    private static readonly Regex GroupStartPattern = new(@"^-$");
    
    /// <summary>
    /// 解析翻译数据文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>解析结果</returns>
    public TranslationData Parse(string filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            return Parse(lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"读取翻译文件失败: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 解析翻译数据文件
    /// </summary>
    /// <param name="lines">文件行</param>
    /// <returns>解析结果</returns>
    public TranslationData Parse(string[] lines)
    {
        var result = new TranslationData();
        
        if (lines.Length == 0)
            return result;
        
        // 第1行: 未知参数
        result.UnknownParam = lines[0].Trim();
        
        int lineIndex = 1;
        
        // 解析分组区域
        lineIndex = ParseGroups(lines, lineIndex, result);
        
        // 解析注释区域
        lineIndex = ParseComments(lines, lineIndex, result);
        
        // 解析数据区域
        ParseDataArea(lines, lineIndex, result);
        
        return result;
    }
    
    /// <summary>
    /// 解析分组区域
    /// </summary>
    private int ParseGroups(string[] lines, int startIndex, TranslationData result)
    {
        int i = startIndex;
        
        // 找到第一个分组开始标记 "-"
        while (i < lines.Length && !GroupStartPattern.IsMatch(lines[i]))
        {
            i++;
        }
        
        if (i >= lines.Length)
            return i;
        
        i++; // 跳过 "-"
        
        int groupIndex = 1;
        
        // 解析每个分组直到遇到下一个 "-" 或注释区域
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            
            // 分组结束
            if (GroupStartPattern.IsMatch(line))
            {
                i++;
                break;
            }
            
            // 注释区域开始（空行后是实际数据）
            if (string.IsNullOrEmpty(line))
            {
                // 继续寻找数据区域开始标记
                i++;
                continue;
            }
            
            // 防越界保护。如果遇到了图片名区域，说明文件缺少闭合的分组 '-' 标记，强制跳出
            if (ImagePattern.IsMatch(line))
            {
                break;
            }
            
            // 分组名称
            result.Groups.Add(new GroupInfo
            {
                Index = groupIndex,
                Name = line
            });
            
            groupIndex++;
            i++;
        }
        
        return i;
    }
    
    /// <summary>
    /// 解析注释区域
    /// </summary>
    private int ParseComments(string[] lines, int startIndex, TranslationData result)
    {
        int i = startIndex;
        
        // 跳过空行
        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }
        
        // 解析注释直到遇到数据区域
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            
            // 数据区域开始
            if (ImagePattern.IsMatch(line))
            {
                break;
            }
            
            // 空行
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }
            
            // 注释行
            result.Comments.Add(line);
            i++;
        }
        
        return i;
    }
    
    /// <summary>
    /// 解析数据区域
    /// </summary>
    private void ParseDataArea(string[] lines, int startIndex, TranslationData result)
    {
        string? currentImageName = null;
        List<LabelItem>? currentLabels = null;
        
        // 用于保存当前正在解析的标注项及其多行文本
        LabelItem? currentLabelItem = null;
        StringBuilder currentText = new StringBuilder();
        
        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // 1. 匹配图片文件名
            var imageMatch = ImagePattern.Match(line);
            if (imageMatch.Success)
            {
                // 在切换图片前，保存上一个正在读取的标注项
                SaveCurrentLabel(currentLabels, ref currentLabelItem, currentText);
                
                // 保存上一张图片的数据
                if (currentImageName != null && currentLabels != null)
                {
                    result.ImageLabels[currentImageName] = currentLabels;
                }
                
                // 开始新的图片
                currentImageName = imageMatch.Groups[1].Value;
                currentLabels = new List<LabelItem>();
                continue;
            }
            
            // 2. 匹配标注数据头部
            var labelMatch = LabelPattern.Match(line);
            if (labelMatch.Success && currentLabels != null)
            {
                // 在开始新标注前，保存上一个正在读取的标注项
                SaveCurrentLabel(currentLabels, ref currentLabelItem, currentText);
                
                var textIndex = int.Parse(labelMatch.Groups[1].Value);
                var x = double.Parse(labelMatch.Groups[2].Value);
                var y = double.Parse(labelMatch.Groups[3].Value);
                var groupIndex = int.Parse(labelMatch.Groups[4].Value);
                
                // 初始化新的标注对象（此时还不确定文本内容）
                currentLabelItem = new LabelItem
                {
                    ImageName = currentImageName!,
                    TextIndex = textIndex,
                    X = x,
                    Y = y,
                    GroupIndex = groupIndex,
                    Text = string.Empty // 稍后填充
                };
                
                continue;
            }
            
            // 3. 如果既不是图片名，也不是标签头，且当前有正在解析的标签，那这就是多行文本的一部分
            if (currentLabelItem != null)
            {
                currentText.AppendLine(line);
            }
        }
        
        // 文件结束时，保存最后遗留的数据
        SaveCurrentLabel(currentLabels, ref currentLabelItem, currentText);
        
        if (currentImageName != null && currentLabels != null)
        {
            result.ImageLabels[currentImageName] = currentLabels;
        }
    }

    private void SaveCurrentLabel(List<LabelItem>? currentLabels, ref LabelItem? currentLabelItem, StringBuilder currentText)
    {
        if (currentLabelItem != null && currentLabels != null)
        {
            // TrimEnd() 可以去除多行文本末尾多余的空行（比如标签之间的那个空行）
            currentLabelItem.Text = currentText.ToString().TrimEnd();
            currentLabels.Add(currentLabelItem);
            
            // 重置状态
            currentLabelItem = null;
            currentText.Clear();
        }
    }
    
    /// <summary>
    /// 根据图片名和分组获取标注
    /// </summary>
    public List<LabelItem> GetLabelsByGroup(TranslationData data, string imageName, int groupIndex)
    {
        if (!data.ImageLabels.TryGetValue(imageName, out var labels))
            return new List<LabelItem>();
        
        return labels.Where(l => l.GroupIndex == groupIndex).ToList();
    }
    
    /// <summary>
    /// 根据图片名获取所有标注
    /// </summary>
    public List<LabelItem> GetLabelsByImage(TranslationData data, string imageName)
    {
        return data.ImageLabels.TryGetValue(imageName, out var labels) 
            ? labels 
            : new List<LabelItem>();
    }

    /// <summary>
    /// 保存翻译数据到文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">翻译数据</param>
    public void Save(string filePath, TranslationData data)
    {
        try
        {
            var content = GenerateFileContent(data);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 根据翻译数据生成文件内容
    /// </summary>
    /// <param name="data">翻译数据</param>
    /// <returns>文件内容字符串</returns>
    public string GenerateFileContent(TranslationData data)
    {
        var sb = new StringBuilder();

        // 1. 写入未知参数 (第一行)
        sb.AppendLine(data.UnknownParam ?? string.Empty);

        // 2. 写入分组区域 (如果有分组)
        if (data.Groups.Any())
        {
            sb.AppendLine("-"); // 分组区域开始标记
            
            foreach (var group in data.Groups.OrderBy(g => g.Index))
            {
                sb.AppendLine(group.Name);
            }
            
            sb.AppendLine("-"); // 分组区域结束标记
        }
        else
        {
            // 没有分组名称
            sb.AppendLine("-");
            sb.AppendLine("-");
        }

        // 3. 写入注释区域
        if (data.Comments.Any())
        {
            foreach (var comment in data.Comments)
            {
                sb.AppendLine(comment);
            }
        }

        // 4. 写入数据区域
        // 文件头与数据区域之间的空行
        if (data.ImageLabels.Any())
        {
            sb.AppendLine();
        }

        bool isFirstImage = true;
        foreach (var kvp in data.ImageLabels)
        {
            if (!isFirstImage)
            {
                sb.AppendLine(); // 图片之间的空行
            }
            isFirstImage = false;

            var imageName = kvp.Key;
            var labels = kvp.Value;

            // 图片头: >>>>>>>>[01.jpeg]<<<<<<<< (即使没有标注也写入，用于标定哪些图片在项目中)
            sb.AppendLine($">>>>>>>>[{imageName}]<<<<<<<<");

            // 空图片：不添加额外空行
            if (labels == null || labels.Count == 0)
            {
                continue;
            }

            // 重新生成标注序号（从1开始）
            int textIndex = 1;
            foreach (var label in labels)
            {
                // 标签头: ----------------[1]----------------[0.252,0.077,1]
                // 归一化坐标保留三位小数，使用 InvariantCulture 确保格式一致
                string xStr = label.X.ToString("F3", CultureInfo.InvariantCulture);
                string yStr = label.Y.ToString("F3", CultureInfo.InvariantCulture);

                sb.AppendLine($"----------------[{textIndex}]----------------[{xStr},{yStr},{label.GroupIndex}]");

                // 标签文本 (多行文本原样输出)
                sb.AppendLine(label.Text);

                // 每个标注后留一空行
                sb.AppendLine();
                textIndex++;
            }
        }

        return sb.ToString();
    }
}
