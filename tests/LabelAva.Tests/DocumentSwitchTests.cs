using System;
using System.IO;
using System.Text;
using Avalonia.Headless.XUnit;
using LabelAva.Tests.Fixtures;

namespace LabelAva.Tests;

/// <summary>
/// 在已打开文档的情况下再打开另一个翻译文件。
///
/// 原先这条路径**不会关闭当前文档**：画布不 ClearCanvas、Navigation 不清，
/// 而 CurrentImageIndex 若仍是 0，InitializeNavigation 里的 `= 0` 不产生变更通知
/// （[ObservableProperty] 只在值真的变化时才回调），于是 CurrentImageChanged 不触发、
/// 新文档的图片永远不加载 —— 现象就是「打开第二个翻译文件后图片不刷新，手动翻页后才生效」。
/// 首次打开之所以正常，是因为索引从初值 -1 变成 0，确实发生了变化。
/// </summary>
public class DocumentSwitchTests
{
    /// <summary>只引用 02.png 的翻译文件：首图（索引 0）与第一个文档不同。</summary>
    private const string SecondTranslationContent =
        "1,0\n" +
        "-\n" +
        "框内\n" +
        "-\n" +
        "Default Comment\n" +
        "\n" +
        ">>>>>>>>[02.png]<<<<<<<<\n" +
        "----------------[1]----------------[0.500,0.500,1]\n" +
        "第二份文档\n" +
        "\n";

    [AvaloniaFact]
    public void 打开第二个翻译文件应当刷新画布图片()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);
        using var harness = new MainWindowHarness(appData);
        var vm = harness.Vm;

        // 第一个文档：首图 01.png，索引停在 0
        var firstOpen = vm.Document.OpenTranslationFileAsync(project.TranslationPath);
        HeadlessPump.Until(() => firstOpen.IsCompleted, "打开第一个翻译文件完成");
        firstOpen.GetAwaiter().GetResult();
        HeadlessPump.Until(
            () => harness.Window.CanvasControl.CurrentImagePath?.EndsWith("01.png", StringComparison.Ordinal) == true,
            "第一个文档的图片已加载");

        // 第二个文档：首图 02.png，索引同样是 0 —— 正是原先不触发变更通知的情形
        var secondPath = Path.Combine(project.Root, "second.txt");
        File.WriteAllText(secondPath, SecondTranslationContent, new UTF8Encoding(false));

        var secondOpen = vm.Document.OpenTranslationFileAsync(secondPath);
        HeadlessPump.Until(() => secondOpen.IsCompleted, "打开第二个翻译文件完成");
        secondOpen.GetAwaiter().GetResult();

        HeadlessPump.Until(
            () => harness.Window.CanvasControl.CurrentImagePath?.EndsWith("02.png", StringComparison.Ordinal) == true,
            "第二个文档的图片已加载",
            diagnostics: () => $"画布当前图片={harness.Window.CanvasControl.CurrentImagePath ?? "<null>"}；" +
                               $"导航索引={vm.Navigation.CurrentImageIndex}；" +
                               $"导航图片数={vm.Navigation.ImageNames.Count}");

        Assert.EndsWith("02.png", harness.Window.CanvasControl.CurrentImagePath!, StringComparison.Ordinal);
    }
}
