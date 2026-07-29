using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using FluentIcons.Avalonia;
using FluentIcons.Common;

namespace LabelAva.Views.Dialogs;

/// <summary>
/// 删除图片的二次确认对话框。代码构建，遵循项目现有确认弹窗模式。
/// </summary>
internal class DeleteImageConfirmDialog : Window
{
    public bool IsConfirmed { get; private set; }

    public DeleteImageConfirmDialog(string imageName, int labelCount)
    {
        Title = "确认删除";
        Width = 420;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
        Background = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush");

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var contentPanel = new DockPanel { Margin = new Thickness(24, 20, 24, 12) };

        var warningIcon = new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Warning,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = 48,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        DockPanel.SetDock(warningIcon, Dock.Left);
        contentPanel.Children.Add(warningIcon);

        var message = labelCount > 0
            ? $"确定删除 {imageName}？\n该图片的 {labelCount} 条标注将被永久删除。"
            : $"确定删除 {imageName}？\n该图片没有标注，删除后不可恢复。";

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        };
        contentPanel.Children.Add(textBlock);

        Grid.SetRow(contentPanel, 0);
        rootGrid.Children.Add(contentPanel);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6,
            Margin = new Thickness(0, 12, 16, 16),
        };

        var cancelButton = new Button { Content = "取消", Width = 80 };
        var deleteButton = new Button
        {
            Content = "删除",
            Width = 80,
            Background = Avalonia.Media.Brush.Parse("#F44336"),
            Foreground = Avalonia.Media.Brushes.White,
        };

        cancelButton.Click += (s, args) => { IsConfirmed = false; Close(); };
        deleteButton.Click += (s, args) => { IsConfirmed = true; Close(); };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(deleteButton);

        Grid.SetRow(buttonPanel, 1);
        rootGrid.Children.Add(buttonPanel);

        Content = rootGrid;
    }
}
