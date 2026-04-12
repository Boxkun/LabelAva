using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            // 设置关闭模式：主窗口关闭时退出进程
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            
            var warmUp = new FluentIcons.Avalonia.FluentIcon { Icon = FluentIcons.Common.Icon.Warning };
        }

        base.OnFrameworkInitializationCompleted();
    }
}