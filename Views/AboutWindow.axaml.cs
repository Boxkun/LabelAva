using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LabelAva.Views;



public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{BuildInfo.Version} ({BuildInfo.CommitHash})";
        BuildTimeText.Text = $"构建日期 {BuildInfo.BuildTime}";
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenRepo(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Boxkun/LabelAva",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
