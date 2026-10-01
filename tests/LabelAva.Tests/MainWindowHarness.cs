using System;
using LabelAva.ViewModels;

namespace LabelAva.Tests;

/// <summary>
/// 一次测试用的 MainWindow 环境：隔离的 AppData + 已显示且初始化完成的窗口。
///
/// 三点必须注意：
/// 1) 必须在 Show() 之后泵调度器，等 InitializeAsync 把各 ViewModel 注入完
///    （注入发生在异步初始化里，不是构造函数里）。
/// 2) 清理时只能 Hide()，**不能 Close()** —— MainWindow.OnWindowClosing 末尾会
///    Environment.Exit(0) 强制退出进程，Close 会直接杀掉测试宿主。
/// 3) AppData 可以由调用方先建好（例如需要先写入 config.json / dlig_conf 的夹具），
///    此时 harness 不负责释放它。
/// </summary>
public sealed class MainWindowHarness : IDisposable
{
    private readonly bool _ownsAppData;

    public MainWindowHarness()
        : this(null)
    {
    }

    public MainWindowHarness(TempAppData? appData)
    {
        _ownsAppData = appData is null;
        AppData = appData ?? new TempAppData();

        Window = new MainWindow();
        Window.Show();

        HeadlessPump.Until(
            () => Window.ViewModel.Document is not null
                  && Window.ViewModel.History is not null
                  && Window.ViewModel.Navigation is not null
                  && Window.ViewModel.Edit is not null
                  && Window.ViewModel.CanvasWorkspace is not null,
            "MainWindow 异步初始化完成");
    }

    public TempAppData AppData { get; }

    public MainWindow Window { get; }

    public MainWindowViewModel Vm => Window.ViewModel;

    public void Dispose()
    {
        Window.Hide();

        if (_ownsAppData)
            AppData.Dispose();
    }
}
