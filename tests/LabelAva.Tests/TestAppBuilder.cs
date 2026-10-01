using Avalonia;
using Avalonia.Headless;
using LabelAva;

[assembly: AvaloniaTestApplication(typeof(LabelAva.Tests.TestAppBuilder))]

namespace LabelAva.Tests;

/// <summary>
/// headless 测试用的 Avalonia 应用构建器。
/// 与 <see cref="Program.BuildAvaloniaApp"/> 的差异只有一处：用 UseHeadless 取代 UsePlatformDetect
/// （以及不启用开发者工具），其余保持一致，避免测试环境与真实运行环境出现无关偏差。
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .WithInterFont();
}
