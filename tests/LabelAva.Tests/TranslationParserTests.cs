using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LabelAva.Models;
using LabelAva.Services;
using Xunit;

namespace LabelAva.Tests;

/// <summary>
/// 翻译文件的换行符处理。
///
/// **决策：读入对 LF / CRLF 不敏感；写出沿用平台换行，刻意不统一。**
/// 依据：本项目的使用对象是一个非跨平台、且对换行差异不敏感的场景；只要我们自己读得进来，
/// 与其他软件之间的换行兼容是对方的事。所以这里不做「统一成 \n」之类的改动。
///
/// 读入侧之所以不敏感，是因为 TranslationParser 用 <c>File.ReadAllLines</c> —— 它按 CRLF / LF / CR
/// 三种都切，并去掉行尾终结符。若哪天有人把它改成「读全文再按 '\n' 切」，CRLF 文件会在每行末尾
/// 留下 '\r'，标记文本会莫名多出一个回车。下面这两条就是拦住这种改法的。
///
/// 已知且有意的平台差异：**多行**标记文本由解析器用 <c>StringBuilder.AppendLine</c> 拼接，
/// 连接符是 <c>Environment.NewLine</c>，所以同一文件在 Windows 上得到 "\r\n"、在其他平台得到 "\n"。
/// </summary>
public class TranslationParserTests
{
    [Fact]
    public void LF与CRLF两种换行解析结果相同()
    {
        var crlf = ParseWith("\r\n");
        var lf = ParseWith("\n");

        Assert.Equal(Snapshot(lf), Snapshot(crlf));
    }

    [Fact]
    public void CRLF文件不会在单行标记文本里留下回车()
    {
        var data = ParseWith("\r\n");

        var labels = data.ImageLabels["01.png"];

        // 单行文本必须逐字相等：ReadAllLines 应当已经吃掉了 \r
        Assert.Equal("第一句", labels[0].Text);
        Assert.Equal("第二句，带标点！", labels[1].Text);

        // 多行文本只要求确实换行了（连接符是平台相关的，见类注释）
        Assert.Contains('\n', labels[2].Text);
    }

    [Fact]
    public void 解析后保存再解析结果不变()
    {
        var parser = new TranslationParser();
        var dir = NewTempDir();
        try
        {
            var first = Path.Combine(dir, "first.txt");
            var second = Path.Combine(dir, "second.txt");

            // 故意喂 CRLF 版本：解析 → 保存（按平台换行写）→ 再解析，数据必须一模一样
            File.WriteAllText(first, SampleContent.Replace("\n", "\r\n"), new UTF8Encoding(false));

            var parsed = parser.Parse(first);
            parser.Save(second, parsed);
            var reparsed = parser.Parse(second);

            Assert.Equal(Snapshot(parsed), Snapshot(reparsed));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>把解析结果摊平成可比较的字符串序列（顺序固定）。</summary>
    private static List<string> Snapshot(TranslationData data)
        => data.ImageLabels
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Select(label =>
                $"{entry.Key}|{label.TextIndex}|{label.GroupIndex}|{label.X}|{label.Y}|{label.Text}"))
            .ToList();

    private static TranslationData ParseWith(string newline)
    {
        var dir = NewTempDir();
        try
        {
            var path = Path.Combine(dir, "test.txt");
            var content = newline == "\n" ? SampleContent : SampleContent.Replace("\n", "\r\n");
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return new TranslationParser().Parse(path);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "labelava-parser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 一份最小但结构完整的翻译文件：参数区 / 分组区 / 注释区 / 图片头 + 三个标记
    /// （第三个是多行文本，用来验证换行符处理）。换行统一写成 \n，需要 CRLF 时整体替换。
    /// </summary>
    private const string SampleContent =
        "1,0\n" +
        "-\n" +
        "框内\n" +
        "框外\n" +
        "-\n" +
        "Default Comment\n" +
        "\n" +
        ">>>>>>>>[01.png]<<<<<<<<\n" +
        "----------------[1]----------------[0.250,0.100,1]\n" +
        "第一句\n" +
        "\n" +
        "----------------[2]----------------[0.500,0.500,1]\n" +
        "第二句，带标点！\n" +
        "\n" +
        "----------------[3]----------------[0.750,0.900,2]\n" +
        "第三句第一行\n" +
        "第三句第二行\n" +
        "\n";
}
