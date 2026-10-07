using Avalonia;
using Avalonia.Headless;
using LabelAva;

[assembly: AvaloniaTestApplication(typeof(LabelAva.Tests.TestAppBuilder))]

namespace LabelAva.Tests;

/// <summary>
/// headless 测试用的 Avalonia 应用构建器。
///
/// 关键点是 <c>UseSkia()</c> + <c>UseHeadlessDrawing = false</c>：
/// 若只用 <c>UseHeadless(UseHeadlessDrawing = true)</c>，平台会自带一个**桩字体管理器** ——
/// 任何按家族名的系统字体查询都会「成功」但返回内置最小字体（FamilyName = BareMinimum），
/// 于是依赖真实字体的代码（例如连字字体校验）在测试里永远走不到成功分支。
/// 交给 Skia 提供字体与文本度量后，系统字体查询才是真实结果。
///
/// 与 <see cref="Program.BuildAvaloniaApp"/> 的其余差异只有：用 UseHeadless 取代 UsePlatformDetect、
/// 不启用开发者工具。
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
