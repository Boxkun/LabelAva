using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LabelAva.Views;



public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Commit {BuildInfo.CommitHash}";
        BuildTimeText.Text = $"构建日期 {BuildInfo.BuildTime}";
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
