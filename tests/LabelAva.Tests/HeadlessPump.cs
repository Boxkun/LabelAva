using System;
using System.Diagnostics;
using System.Threading;
using Avalonia.Headless;
using Avalonia.Threading;

namespace LabelAva.Tests;

/// <summary>
/// headless 下的调度器泵。
///
/// MainWindow 的重初始化跑在 `async void OnWindowFirstOpened` 里：
/// 它先 await 一个 Render 优先级的 InvokeAsync，再 await InitializeAsync()，
/// 而 InitializeAsync 第一步就是 `await Task.Run(...)`，续体需要回到 UI 线程执行排队作业。
/// 测试线程就是 UI 线程，所以必须主动 RunJobs 才能把这条链推完；
/// 只 Show() 然后立刻断言会看到 VM 尚未注入（Document/History 等仍是 null）。
/// </summary>
public static class HeadlessPump
{
    /// <summary>
    /// 泵调度器直到条件成立。超时信息里可以附带一段诊断文本 ——
    /// 平台相关的问题本地往往复现不了，只能靠 CI 上的失败信息定位。
    /// </summary>
    public static void Until(
        Func<bool> condition,
        string description,
        int timeoutMs = 15000,
        Func<string>? diagnostics = null)
    {
        if (condition()) return;

        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                var extra = diagnostics?.Invoke();
                var detail = string.IsNullOrEmpty(extra)
                    ? string.Empty
                    : $"{Environment.NewLine}诊断: {extra}";
                throw new TimeoutException($"等待「{description}」超时（{timeoutMs}ms）{detail}");
            }

            // 执行 UI 线程上排队的工作（初始化续体、Dispatcher.UIThread.Post 的光标修复等）
            Dispatcher.UIThread.RunJobs();

            // 推进 headless 渲染定时器，让 Render 优先级的工作有机会执行
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

            // 让出一点时间给线程池上的 Task.Run 完成并投递续体
            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// 固定轮次地把排队工作泵空，用于每个场景步骤之后取快照前。
    ///
    /// 刻意用「固定轮次 + 极短 sleep」而不是「一直泵到没事可做」，也不长睡：
    /// 崩溃恢复防抖定时器是 200ms 级的真实时间触发，睡太久会让
    /// 「定时器这一轮到底触发了没有」变成非确定因素，轨迹就不再可复现。
    /// </summary>
    public static void Drain(int rounds = 8, int sleepMs = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Thread.Sleep(sleepMs);
        }
    }
}
