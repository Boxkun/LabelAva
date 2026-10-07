using Avalonia;
using Avalonia.Headless.XUnit;
using LabelAva.Services;
using LabelAva.Views;

namespace LabelAva.Tests;

/// <summary>
/// 窗口尺寸/位置的状态与持久化策略。
///
/// 这些规则原本散在 MainWindow 的构造函数、首帧处理、防抖回调与关闭流程四处、且绑在窗口实例上，
/// 无法单测。其中最容易写错的一条是「最大化时保存的必须是 Normal 尺寸，而不是膨胀后的当前尺寸」——
/// 写错的话用户每次最大化后关闭窗口，下次启动窗口就会变成满屏大小且再也回不去。
/// </summary>
public class WindowStateTrackerTests
{
    [AvaloniaFact]
    public void 屏幕内的位置判定为可用()
    {
        using var appData = new TempAppData();
        var tracker = new WindowStateTracker(CreateProvider(1200, 800, 100, 100, maximized: false));

        Assert.True(tracker.HasUsableSavedPosition(DefaultScreens));
    }

    [AvaloniaFact]
    public void 负坐标的位置判定为不可用()
    {
        using var appData = new TempAppData();
        var tracker = new WindowStateTracker(CreateProvider(1200, 800, -50, 100, maximized: false));

        Assert.False(tracker.HasUsableSavedPosition(DefaultScreens));
    }

    [AvaloniaFact]
    public void 完全移出屏幕的位置判定为不可用()
    {
        using var appData = new TempAppData();
        var tracker = new WindowStateTracker(CreateProvider(800, 600, 5000, 5000, maximized: false));

        Assert.False(tracker.HasUsableSavedPosition(DefaultScreens));
    }

    [AvaloniaFact]
    public void 最大化时持久化的是Normal边界而不是膨胀后的边界()
    {
        using var appData = new TempAppData();
        var provider = CreateProvider(1000, 700, 10, 20, maximized: false);
        var tracker = new WindowStateTracker(provider);

        // 模拟：窗口已最大化，当前边界是整屏尺寸
        tracker.Persist(new WindowBounds(1920, 1080, new PixelPoint(0, 0)), isMaximized: true);

        Assert.True(provider.Current.WindowMaximized);
        Assert.Equal(1000, provider.Current.WindowWidth);
        Assert.Equal(700, provider.Current.WindowHeight);
        Assert.Equal(10, provider.Current.WindowX);
        Assert.Equal(20, provider.Current.WindowY);
    }

    [AvaloniaFact]
    public void 非最大化时持久化当前边界()
    {
        using var appData = new TempAppData();
        var provider = CreateProvider(1000, 700, 10, 20, maximized: true);
        var tracker = new WindowStateTracker(provider);

        tracker.Persist(new WindowBounds(1234, 777, new PixelPoint(33, 44)), isMaximized: false);

        Assert.False(provider.Current.WindowMaximized);
        Assert.Equal(1234, provider.Current.WindowWidth);
        Assert.Equal(777, provider.Current.WindowHeight);
        Assert.Equal(33, provider.Current.WindowX);
        Assert.Equal(44, provider.Current.WindowY);
    }

    [AvaloniaFact]
    public void 记住新的Normal边界后_最大化持久化使用新值()
    {
        using var appData = new TempAppData();
        var provider = CreateProvider(1000, 700, 10, 20, maximized: false);
        var tracker = new WindowStateTracker(provider);

        // 模拟：用户在非最大化状态下拖动/缩放了窗口，防抖后记录
        tracker.RememberNormalBounds(new WindowBounds(1100, 750, new PixelPoint(50, 60)));
        tracker.Persist(new WindowBounds(1920, 1080, new PixelPoint(0, 0)), isMaximized: true);

        Assert.Equal(1100, provider.Current.WindowWidth);
        Assert.Equal(750, provider.Current.WindowHeight);
        Assert.Equal(50, provider.Current.WindowX);
        Assert.Equal(60, provider.Current.WindowY);
    }

    [AvaloniaFact]
    public void 持久化后可被新实例读回()
    {
        using var appData = new TempAppData();
        var provider = CreateProvider(1000, 700, 10, 20, maximized: false);
        new WindowStateTracker(provider)
            .Persist(new WindowBounds(1440, 900, new PixelPoint(123, 45)), isMaximized: false);

        // 模拟重启：重新从磁盘加载
        var reloaded = new AppSettingsProvider();
        reloaded.Load();
        var restored = new WindowStateTracker(reloaded);

        Assert.Equal(1440, restored.NormalBounds.Width);
        Assert.Equal(900, restored.NormalBounds.Height);
        Assert.Equal(123, restored.NormalBounds.Position.X);
        Assert.Equal(45, restored.NormalBounds.Position.Y);
        Assert.False(restored.RestoreMaximized);
    }

    [AvaloniaFact]
    public void 恢复最大化标志来自设置()
    {
        using var appData = new TempAppData();
        var tracker = new WindowStateTracker(CreateProvider(1000, 700, 10, 20, maximized: true));

        Assert.True(tracker.RestoreMaximized);
    }

    private static readonly PixelRect[] DefaultScreens = { new(0, 0, 1920, 1080) };

    private static AppSettingsProvider CreateProvider(
        double width, double height, int x, int y, bool maximized)
    {
        // 不调用 Load()：直接用默认设置，避免依赖磁盘状态
        var provider = new AppSettingsProvider();
        var settings = provider.Current;
        settings.WindowWidth = width;
        settings.WindowHeight = height;
        settings.WindowX = x;
        settings.WindowY = y;
        settings.WindowMaximized = maximized;
        return provider;
    }
}
