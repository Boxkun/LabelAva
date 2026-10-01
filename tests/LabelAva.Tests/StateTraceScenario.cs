using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LabelAva.Models;
using LabelAva.Tests.Fixtures;

namespace LabelAva.Tests;

/// <summary>轨迹中的一步：步骤名 + 该步之后的状态快照。</summary>
public sealed record TraceStep(string Step, string Json);

/// <summary>
/// 固定场景脚本：把「打开文档 → 选中 → 编辑模式 → 分组 → 快捷输入（追加/替换选区）
/// → 快捷键 → 切图 → 关闭文档」跑一遍，每步取一次状态快照。
///
/// 这个场景是后续所有「行为保持型重构」的验收依据：拆分前后轨迹必须逐字节一致。
/// 驱动方式刻意贴近真实路径 —— 走 VM 命令、走真实 RoutedEvent、走真实键盘事件，
/// 而不是直接调私有方法，这样 MainWindow 那层视图胶水才会真正被覆盖。
/// </summary>
public static class StateTraceScenario
{
    public static List<TraceStep> Run(MainWindowHarness harness, ProjectFixture project)
    {
        var steps = new List<TraceStep>();
        var window = harness.Window;
        var vm = harness.Vm;

        void Step(string name)
        {
            HeadlessPump.Drain();
            steps.Add(new TraceStep(name, StateTrace.Capture(harness, project)));
        }

        Step("01-初始化完成");

        // 直接给路径，绕过文件选择对话框
        var openTask = vm.Document.OpenTranslationFileAsync(project.TranslationPath);
        HeadlessPump.Until(() => openTask.IsCompleted, "打开翻译文件完成");
        openTask.GetAwaiter().GetResult();
        Step("02-打开文档");

        vm.Navigation.SelectLabelByIndex(1);
        Step("03-选中标记1");

        vm.Edit.ToggleEditModeCommand.Execute(null);
        Step("04-进入编辑模式");

        vm.Edit.SwitchGroupCommand.Execute(2);
        Step("05-切到分组2");

        var dligButton = UiDriver.FindDligQuickInputButton(window);
        UiDriver.Click(dligButton);
        Step("06-快捷输入追加");

        // 选中全部文本后再次点同一个按钮：应当替换选区，而不是继续追加。
        // 注意：设完选区必须立即点击，中间不能泵调度器 —— 视图重建链会 POST
        // 光标修复任务把选区收拢到末尾，那样就退化成「插入」了。
        var textBox = window.FindControl<TextBox>("TranslationTextBox")
            ?? throw new InvalidOperationException("找不到 TranslationTextBox");
        UiDriver.SelectAllText(textBox);

        UiDriver.Click(dligButton);
        Step("07-快捷输入替换选区");

        // 真实键盘事件，覆盖 ShortcutRouter + 全局快捷键路由（Ctrl+D1 → 切回分组 1）
        window.KeyPress(Key.D1, RawInputModifiers.Control, PhysicalKey.Digit1, "1");
        Step("08-快捷键切回分组1");

        vm.Navigation.TrySwitchToImage("02.png");
        Step("09-切到第二张图");

        // 走强制关闭，避免弹「未保存更改」确认对话框（那会打开真窗口把场景挂住）
        vm.Document.ForceCloseDocument();
        Step("10-关闭文档");

        return steps;
    }
}
