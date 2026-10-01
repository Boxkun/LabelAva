using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LabelAva.Tests.Fixtures;

/// <summary>
/// 一次测试用的最小项目夹具：一个翻译文件 + 两张真实 PNG，
/// 以及一份把连字配置指向 <c>TestFont</c> 的 config.json。
///
/// 要点：
/// - 图片文件名必须与翻译文件里的键完全一致，否则 ImageValidationService 会判定
///   Missing/FormatIssue，DocumentViewModel 会去弹「图片关联管理器」真窗口，场景就卡住了。
/// - 连字配置故意把 fontFamily 留空：这样 ApplyDligConfig 走「字体名为空」分支，
///   既能覆盖快捷输入按钮的装载路径，又不依赖具体机器上装了哪个字体（保证轨迹可复现）。
/// - config.json 必须在 MainWindow 构造之前写好（InitializeAsync 里会读它）。
/// </summary>
public sealed class ProjectFixture : IDisposable
{
    public ProjectFixture(TempAppData appData)
    {
        Root = Path.Combine(appData.Root, "project");
        Directory.CreateDirectory(Root);

        WriteImage(Path.Combine(Root, "01.png"), new Rgba32(200, 60, 60));
        WriteImage(Path.Combine(Root, "02.png"), new Rgba32(60, 60, 200));

        TranslationPath = Path.Combine(Root, "test.txt");
        File.WriteAllText(TranslationPath, TranslationContent, new UTF8Encoding(false));

        WriteSettings(appData.Root);
    }

    public string Root { get; }

    public string TranslationPath { get; }

    private static void WriteImage(string path, Rgba32 color)
    {
        using var image = new Image<Rgba32>(16, 16, color);
        image.SaveAsPng(path);
    }

    private static void WriteSettings(string appDataRoot)
    {
        var dligDir = Path.Combine(appDataRoot, "Roaming", "dlig_conf");
        Directory.CreateDirectory(dligDir);

        File.WriteAllText(
            Path.Combine(dligDir, "TestFont.json"),
            """
            {
              "fontFamily": "",
              "fontFeatures": "dlig=1",
              "quickInputs": [
                { "label": "~~", "character": "~~" },
                { "label": "#02", "character": "#02" },
                { "label": "@01", "character": "@01" }
              ]
            }
            """,
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(appDataRoot, "Roaming", "config.json"),
            """
            {
              "Version": "0.3.0",
              "ActiveDligConfig": "TestFont",
              "AutoFocusTextBox": true,
              "WindowWidth": 1200,
              "WindowHeight": 800,
              "WindowX": 0,
              "WindowY": 0
            }
            """,
            new UTF8Encoding(false));
    }

    /// <summary>
    /// 翻译文件内容。首行是未知参数区，随后是分组区（"-" 包裹）、注释区、
    /// 再是数据区：图片头 → 若干「标签头 + 多行文本」。
    /// </summary>
    private const string TranslationContent = """
        1,0
        -
        框内
        框外
        -
        Default Comment

        >>>>>>>>[01.png]<<<<<<<<
        ----------------[1]----------------[0.250,0.100,1]
        第一句

        ----------------[2]----------------[0.500,0.500,1]
        第二句，带标点！

        ----------------[3]----------------[0.750,0.900,2]
        第三句第一行
        第三句第二行

        >>>>>>>>[02.png]<<<<<<<<
        ----------------[1]----------------[0.100,0.100,1]
        二号图第一句

        ----------------[2]----------------[0.900,0.900,2]
        二号图第二句

        """;

    public void Dispose()
    {
        // 目录由 TempAppData 统一清理，这里无需处理
    }
}
