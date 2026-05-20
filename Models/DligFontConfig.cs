using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LabelAva.Models;

public class DligFontConfig
{
    [JsonPropertyName("fontFamily")]
    public string FontFamily { get; set; } = "";

    [JsonPropertyName("fontFeatures")]
    public string FontFeatures { get; set; } = "dlig=1";

    [JsonPropertyName("quickInputs")]
    public List<QuickInputSlot> QuickInputs { get; set; } = new();
}

public class QuickInputSlot
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("character")]
    public string Character { get; set; } = "";

    /// <summary>
    /// 运行时标记：来自连字配置则为 true，默认快捷输入则为 false。不序列化。
    /// </summary>
    [JsonIgnore]
    public bool IsFromDligConfig { get; set; }

    /// <summary>
    /// 用于显示的 label，包裹 U+2068/U+2069 (FSI/PDI) 以隔离 BiDi 配对上下文。
    /// 避免开括号/开引号字符在独立 TextBlock 中因 HarfBuzz 配对查找而显示空白。
    /// </summary>
    [JsonIgnore]
    public string DisplayLabel => "\u2068" + (string.IsNullOrEmpty(Label) ? Character : Label) + "\u2069";
}
