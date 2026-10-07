using System.Collections.Generic;
using Avalonia;
using LabelAva.Services;

namespace LabelAva.Views;

/// <summary>窗口的 Normal（非最大化）尺寸与位置。</summary>
public readonly record struct WindowBounds(double Width, double Height, PixelPoint Position);

/// <summary>
/// 窗口尺寸/位置的状态与持久化策略。
///
/// 抽出来的理由：这段逻辑原本散在 MainWindow 的构造函数、首帧处理、防抖回调与关闭流程四处，
/// 且依赖窗口实例，无法单测。关键的一条策略是「最大化时保存的是 Normal 尺寸而不是膨胀后的尺寸」——
/// 这类容易写错的规则放在一个能直接断言的类里，比散在事件回调里安全得多。
///
/// 视图侧仍保留的只有「何时调用」：DispatcherTimer 防抖、PropertyChanged 订阅、把结果应用到窗口。
/// </summary>
public sealed class WindowStateTracker
{
    /// <summary>标题栏高度的近似值：只要这一段落在某个屏幕内，就认为位置可用。</summary>
    private const int TitleBarHeight = 60;

    private readonly AppSettingsProvider _settingsProvider;
    private WindowBounds _normalBounds;

    public WindowStateTracker(AppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;

        var settings = settingsProvider.Current;
        RestoreMaximized = settings.WindowMaximized;
        _normalBounds = new WindowBounds(
            settings.WindowWidth,
            settings.WindowHeight,
            new PixelPoint(settings.WindowX, settings.WindowY));
    }

    /// <summary>启动时是否应当切到最大化（延迟到首帧之后再切，让 OS 先记录 Normal 尺寸）。</summary>
    public bool RestoreMaximized { get; }

    /// <summary>当前记录的 Normal 尺寸/位置。</summary>
    public WindowBounds NormalBounds => _normalBounds;

    /// <summary>
    /// 设置里保存的位置在当前屏幕配置下是否可用（负坐标直接判不可用；否则要求标题栏至少可见）。
    /// </summary>
    public bool HasUsableSavedPosition(IReadOnlyList<PixelRect> screens)
        => _normalBounds.Position.X >= 0
           && _normalBounds.Position.Y >= 0
           && IsTitleBarVisible(_normalBounds.Position, _normalBounds.Width, screens);

    /// <summary>标题栏区域是否与任一屏幕相交。</summary>
    public static bool IsTitleBarVisible(PixelPoint position, double width, IReadOnlyList<PixelRect> screens)
    {
        var titleArea = new PixelRect(position, new PixelSize((int)width, TitleBarHeight));
        foreach (var screen in screens)
        {
            if (screen.Intersects(titleArea))
                return true;
        }

        return false;
    }

    /// <summary>防抖后记录 Normal 尺寸（调用方保证当前处于 Normal 状态）。</summary>
    public void RememberNormalBounds(WindowBounds bounds) => _normalBounds = bounds;

    /// <summary>
    /// 决定要写回设置的值：最大化时用记录的 Normal 边界，否则用当前边界。
    /// </summary>
    public WindowBounds ResolveBoundsToPersist(WindowBounds currentBounds, bool isMaximized)
        => isMaximized ? _normalBounds : currentBounds;

    /// <summary>把边界与最大化标志写入设置并落盘。</summary>
    public void Persist(WindowBounds currentBounds, bool isMaximized)
    {
        var bounds = ResolveBoundsToPersist(currentBounds, isMaximized);
        var settings = _settingsProvider.Current;

        settings.WindowMaximized = isMaximized;
        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;
        settings.WindowX = bounds.Position.X;
        settings.WindowY = bounds.Position.Y;

        _settingsProvider.Save();
    }
}
