using System;
using System.IO;
using System.Text;
using Avalonia.Headless.XUnit;
using LabelAva.Tests.Fixtures;

namespace LabelAva.Tests;

/// <summary>
/// 行为轨迹基线测试。
///
/// 用法：
/// - 正常 `dotnet test` 会跑场景并与 __baseline__/state-trace.json 逐行比对，不一致即失败。
/// - 需要（重新）录制基线时设置环境变量 `LABELAVA_RECORD_BASELINE=1` 再跑一次。
///
/// 注意：录制基线必须在**任何重构之前**完成；之后每一刀拆分都只允许轨迹保持不变。
/// </summary>
public class StateTraceBaselineTests
{
    private static string BaselinePath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "__baseline__", "state-trace.json"));

    [AvaloniaFact]
    public void 状态轨迹与基线一致()
    {
        using var appData = new TempAppData();
        using var project = new ProjectFixture(appData);
        using var harness = new MainWindowHarness(appData);

        var steps = StateTraceScenario.Run(harness, project);
        var actual = string.Join(
            Environment.NewLine,
            steps.Select(s => $"### {s.Step}{Environment.NewLine}{s.Json}"));

        if (Environment.GetEnvironmentVariable("LABELAVA_RECORD_BASELINE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
            File.WriteAllText(BaselinePath, actual, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(BaselinePath),
            $"基线文件不存在：{BaselinePath}。请先用 LABELAVA_RECORD_BASELINE=1 录制。");

        var expectedLines = Normalize(File.ReadAllText(BaselinePath));
        var actualLines = Normalize(actual);

        if (expectedLines.Length == actualLines.Length && expectedLines.SequenceEqual(actualLines))
            return;

        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var expected = i < expectedLines.Length ? expectedLines[i] : "<基线缺失该行>";
            var got = i < actualLines.Length ? actualLines[i] : "<实际输出缺失该行>";

            if (expected != got)
            {
                Assert.Fail(
                    $"行为轨迹与基线不一致（第 {i + 1} 行）{Environment.NewLine}" +
                    $"基线: {expected}{Environment.NewLine}" +
                    $"实际: {got}{Environment.NewLine}" +
                    $"基线文件: {BaselinePath}");
            }
        }

        Assert.Fail($"行为轨迹行数不一致：基线 {expectedLines.Length} 行，实际 {actualLines.Length} 行");
    }

    private static string[] Normalize(string text)
        => text.Replace("\r\n", "\n").Split('\n');
}
