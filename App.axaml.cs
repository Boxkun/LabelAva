using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LabelAva.Models;

namespace LabelAva;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            
            // 订阅应用关闭事件，确保窗口正确关闭
            desktop.ShutdownRequested += OnDesktopShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    /// <summary>
    /// 应用关闭时清理资源
    /// </summary>
    private void OnDesktopShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 确保主窗口已关闭并释放资源
            if (desktop.MainWindow != null)
            {
                // 窗口的 Closing 事件会处理资源清理
                desktop.MainWindow.Close();
            }
            
            // 取消订阅
            desktop.ShutdownRequested -= OnDesktopShutdownRequested;
        }
    }
}