using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Layout;
using LabelAva.Services;
using LabelAva.Models;
using LabelAva.Views;
using LabelAva.Commands;
using System.Linq;
using LabelAva.ViewModels;
using System.Diagnostics;
using Avalonia.Input.Platform;


namespace LabelAva;

/// <summary>
/// MainWindow 的对话框编排部分：未保存确认 / 崩溃恢复 / 图片选择 / 文件关联管理器。
/// 这些方法不依赖任何窗口私有状态（只用 this 作为 Owner），所以单独成文件便于定位，
/// 也为后续抽成独立的对话框服务留出位置。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 未保存更改确认对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<UnsavedChangesResult> ShowUnsavedChangesDialogAsync(string message)
    {
        var result = UnsavedChangesResult.Cancel;

        var dialog = new Window
        {
            Title = "保存",
            Width = 420,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None },
            Background = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush") ?? Brushes.White
        };

        // 根布局：上方内容区（*）+ 下方按钮栏（Auto）
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // 内容区：DockPanel 实现左图标 + 右文本并排布局
        var contentPanel = new DockPanel { Margin = new Thickness(24, 20, 24, 12) };

        // 叹号图标（FluentIcon 基于字体渲染，用 FontSize 控制大小）
        var warningIcon = new FluentIcons.Avalonia.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Warning,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = 48,
            // Width = 36,
            // Height = 36,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        DockPanel.SetDock(warningIcon, Dock.Left);
        contentPanel.Children.Add(warningIcon);

        // 提示文本（顶部留出偏移以与图标视觉中心对齐）
        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        };
        contentPanel.Children.Add(textBlock);

        Grid.SetRow(contentPanel, 0);
        rootGrid.Children.Add(contentPanel);

        // 底部按钮区域（含顶部分隔线）
        var buttonArea = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 6,
            Margin = new Thickness(0, 12, 16, 16),
        };

        var saveButton = new Button { Content = "保存", Width = 80 };
        var discardButton = new Button { Content = "不保存", Width = 80 };
        var cancelButton = new Button { Content = "取消", Width = 80 };

        saveButton.Click += (s, e) => { result = UnsavedChangesResult.Save; dialog.Close(); };
        discardButton.Click += (s, e) => { result = UnsavedChangesResult.Discard; dialog.Close(); };
        cancelButton.Click += (s, e) => { result = UnsavedChangesResult.Cancel; dialog.Close(); };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(discardButton);
        buttonPanel.Children.Add(cancelButton);
        buttonArea.Children.Add(buttonPanel);

        Grid.SetRow(buttonArea, 1);
        rootGrid.Children.Add(buttonArea);

        dialog.Content = rootGrid;
        dialog.Measure(new Size(420, 140));
        dialog.Arrange(new Rect(0, 0, 420, 140));

        await dialog.ShowDialog(this);

        return result;
    }

    /// <summary>
    /// 崩溃恢复对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<RecoveryResult> ShowRecoveryDialogAsync(string message)
    {
        var result = RecoveryResult.Discard;

        var dialog = new Window
        {
            Title = "崩溃恢复",
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None },
            Background = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush") ?? Brushes.White
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var contentPanel = new DockPanel { Margin = new Thickness(24, 20, 24, 12) };

        var warningIcon = new FluentIcons.Avalonia.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Warning,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = 48,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        DockPanel.SetDock(warningIcon, Dock.Left);
        contentPanel.Children.Add(warningIcon);

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        };
        contentPanel.Children.Add(textBlock);

        Grid.SetRow(contentPanel, 0);
        rootGrid.Children.Add(contentPanel);

        var buttonArea = new Border { Margin = new Thickness(16, 0, 16, 16) };
        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 12,
        };

        var recoverButton = new Button
        {
            Content = "恢复",
            Width = 80,
            Height = 32,
        };
        recoverButton.Click += (_, _) =>
        {
            result = RecoveryResult.Recover;
            dialog.Close();
        };
        buttonPanel.Children.Add(recoverButton);

        var discardButton = new Button
        {
            Content = "丢弃",
            Width = 80,
            Height = 32,
        };
        discardButton.Click += (_, _) => { dialog.Close(); };
        buttonPanel.Children.Add(discardButton);

        buttonArea.Child = buttonPanel;
        Grid.SetRow(buttonArea, 1);
        rootGrid.Children.Add(buttonArea);

        dialog.Content = rootGrid;
        await dialog.ShowDialog(this);

        return result;
    }

    /// <summary>
    /// 图片选择对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<ImageSelectionResult?> ShowImageSelectionDialogAsync(
        List<string> imageFiles, string defaultFileName)
    {
        var selectionWindow = new Views.ImageSelectionWindow(imageFiles, defaultFileName);
        selectionWindow.Owner = this;

        var dialogResult = await selectionWindow.ShowDialog<bool>(this);

        if (!dialogResult || selectionWindow.SelectedImagePaths.Count == 0)
            return null;

        return new ImageSelectionResult
        {
            SelectedImagePaths = selectionWindow.SelectedImagePaths,
            FileName = selectionWindow.FileName
        };
    }

    /// <summary>
    /// 文件关联管理器对话框（作为回调注入 DocumentViewModel）
    /// </summary>
    private async Task<ImageAssociationResult?> ShowImageAssociationDialogAsync(
        List<ImageAssociationItem> items, string imageFolderPath)
    {
        var associationWindow = new ImageAssociationWindow(items, imageFolderPath);
        var dialogResult = await associationWindow.ShowDialog<bool>(this);
        return dialogResult ? associationWindow.Result : null;
    }

}
