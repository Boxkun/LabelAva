using System;
using Avalonia.Headless.XUnit;
using LabelAva.Services;
using LabelAva.ViewModels;

namespace LabelAva.Tests;

/// <summary>
/// P0 冒烟：确认 MainWindow 能在 headless 下构造、显示并完成异步初始化，
/// 且所有依赖都落在重定向后的临时目录里（不碰真实用户配置）。
/// </summary>
public class SmokeTests
{
    [AvaloniaFact]
    public void MainWindow_可以在headless下构造并完成初始化()
    {
        using var harness = new MainWindowHarness();

        Assert.IsType<MainWindowViewModel>(harness.Window.DataContext);
        Assert.NotNull(harness.Vm.StatusBar);
        Assert.NotNull(harness.Vm.History);
        Assert.NotNull(harness.Vm.Edit);
        Assert.NotNull(harness.Vm.Document);
        Assert.NotNull(harness.Vm.Navigation);
        Assert.NotNull(harness.Vm.CanvasWorkspace);
    }

    [AvaloniaFact]
    public void 构造窗口不会触碰真实用户配置目录()
    {
        using var harness = new MainWindowHarness();

        Assert.StartsWith(harness.AppData.Root, AppDataHelper.AppDataFolder, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(harness.AppData.Root, AppDataHelper.LocalDataFolder, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(harness.AppData.Root, AppDataHelper.SettingsFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(harness.AppData.Root, AppDataHelper.RecoveryFolder, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(harness.AppData.Root, AppDataHelper.DligConfigFolder, StringComparison.OrdinalIgnoreCase);
    }
}
