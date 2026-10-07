using System.IO;
using Avalonia.Headless.XUnit;
using LabelAva.Services;
using LabelAva.Tests.Fixtures;

namespace LabelAva.Tests;

/// <summary>
/// 测试环境的一条底层约束：**夹具图片必须被真实解码**。
///
/// 背景：<c>UseHeadless(UseHeadlessDrawing = true)</c> 时平台自带桩渲染后端，它并不真正解码位图 ——
/// <c>Bitmap.Size</c> 会返回 1×1。而画布的 Fit 缩放是
/// <c>min(容器宽/图宽, 容器高/图高)</c>，图片尺寸被读成 1×1 会让基准缩放被放大约 16 倍
/// （夹具 PNG 的真实边长），行为轨迹里的 currentFitScale / 变换矩阵 / 缩放百分比就会全部失真。
///
/// 所以测试用 <c>UseSkia()</c> + <c>UseHeadlessDrawing = false</c>；
/// 这条测试用来防止有人把它改回桩后端 —— 那时行为轨迹会静默地按 1×1 图片记录，看起来「通过」但值是假的。
/// </summary>
public class FixtureImageTests
{
    [AvaloniaFact]
    public void 夹具图片必须被真实解码为16x16()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);

        using var bitmap = ImageLoader.Load(Path.Combine(project.Root, "01.png"));

        Assert.Equal(16, bitmap.Size.Width);
        Assert.Equal(16, bitmap.Size.Height);
    }
}
