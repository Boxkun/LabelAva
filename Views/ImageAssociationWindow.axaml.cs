using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LabelAva.Models;
using LabelAva.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace LabelAva.Views;

public partial class ImageAssociationWindow : Window
{
    private static readonly Avalonia.Media.IBrush ErrorBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xF4, 0x43, 0x36));
    private static readonly Avalonia.Media.IBrush OkBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x00, 0x00, 0x00));

    private readonly ImageValidationService _validationService = new();
    private ObservableCollection<ImageAssociationItem> _items = new();
    private string _imageFolderPath = string.Empty;

    public ImageAssociationResult? Result { get; private set; }

    public ImageAssociationWindow()
    {
        InitializeComponent();
    }

    public ImageAssociationWindow(List<ImageAssociationItem> items, string imageFolderPath)
    {
        InitializeComponent();

        _imageFolderPath = imageFolderPath;
        _items = new ObservableCollection<ImageAssociationItem>(items);

        AssociationList.ItemsSource = _items;
        FolderPathBox.Text = imageFolderPath;

        foreach (var item in _items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        UpdateStatusSummary();
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageAssociationItem.NewPath) && sender is ImageAssociationItem item)
        {
            RevalidateItem(item);
            UpdateStatusSummary();
        }
    }

    private void UpdateStatusSummary()
    {
        var missingCount = _items.Count(i => i.Status == ImageValidationStatus.Missing);
        if (missingCount > 0)
        {
            StatusSummary.Text = $"{missingCount} 张图片存在问题";
            StatusSummary.Foreground = ErrorBrush;
        }
        else
        {
            StatusSummary.Text = "";
            StatusSummary.Foreground = OkBrush;
        }
    }

    private void RevalidateAllItems()
    {
        var folderPath = FolderPathBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(folderPath)) return;

        _imageFolderPath = folderPath;

        foreach (var item in _items)
        {
            if (string.IsNullOrEmpty(item.NewPath))
            {
                var (status, statusText) = _validationService.ValidateSingleWithText(folderPath, item.ImageName);
                item.Status = status;
                item.StatusText = statusText;
            }
        }

        UpdateStatusSummary();
    }

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择搜索文件夹",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            FolderPathBox.Text = folders[0].Path.LocalPath;
        }
    }

    private void OnFolderPathTextChanged(object? sender, TextChangedEventArgs e)
    {
        RevalidateAllItems();
    }

    private async void OnSelectFile(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not ImageAssociationItem item) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var imageFilter = new[]
        {
            new FilePickerFileType("图片文件") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" } },
            new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
        };

        var startFolder = !string.IsNullOrEmpty(_imageFolderPath) && Directory.Exists(_imageFolderPath)
            ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(_imageFolderPath)
            : null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"选择 {item.ImageName} 的关联文件",
            AllowMultiple = false,
            FileTypeFilter = imageFilter,
            SuggestedStartLocation = startFolder
        });

        if (files.Count > 0)
        {
            item.NewPath = files[0].Path.LocalPath;
            RevalidateItem(item);
            UpdateStatusSummary();
        }
    }

    private void RevalidateItem(ImageAssociationItem item)
    {
        if (!string.IsNullOrEmpty(item.NewPath))
        {
            var exists = File.Exists(item.NewPath);
            item.Status = exists ? ImageValidationStatus.OK : ImageValidationStatus.Missing;
            item.StatusText = exists ? "\u2713 正常" : "\u2717 缺失";
        }
        else
        {
            var (status, statusText) = _validationService.ValidateSingleWithText(_imageFolderPath, item.ImageName);
            item.Status = status;
            item.StatusText = statusText;
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var folderPath = FolderPathBox.Text?.Trim() ?? string.Empty;
        var writeToFile = WriteToFileCheckBox.IsChecked == true;

        var missingItems = _items.Where(i => i.Status != ImageValidationStatus.OK && string.IsNullOrEmpty(i.NewPath)).ToList();
        if (missingItems.Count > 0)
        {
            ShowWarningDialog(missingItems.Count);
            return;
        }

        ConfirmInternal(folderPath, writeToFile);
    }

    private async void ShowWarningDialog(int missingCount)
    {
        var dialog = new Window
        {
            Title = "警告",
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Background = Avalonia.Media.Brushes.White
        };

        var result = false;

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
            Text = $"{missingCount} 张图片仍未关联有效文件。继续加载将不会显示有效图片。",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        };
        contentPanel.Children.Add(textBlock);

        Grid.SetRow(contentPanel, 0);
        rootGrid.Children.Add(contentPanel);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 6,
            Margin = new Thickness(0, 12, 16, 16),
        };

        var continueButton = new Button { Content = "继续", Width = 80 };
        var backButton = new Button { Content = "返回", Width = 80 };

        continueButton.Click += (s, args) => { result = true; dialog.Close(); };
        backButton.Click += (s, args) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(continueButton);
        buttonPanel.Children.Add(backButton);

        Grid.SetRow(buttonPanel, 1);
        rootGrid.Children.Add(buttonPanel);

        dialog.Content = rootGrid;

        await dialog.ShowDialog(this);

        if (result)
        {
            var folderPath = FolderPathBox.Text?.Trim() ?? string.Empty;
            var writeToFile = WriteToFileCheckBox.IsChecked == true;
            ConfirmInternal(folderPath, writeToFile);
        }
    }

    private void ConfirmInternal(string folderPath, bool writeToFile)
    {
        var remappings = new Dictionary<string, string>();
        foreach (var item in _items)
        {
            if (!string.IsNullOrEmpty(item.NewPath))
            {
                remappings[item.ImageName] = item.NewPath;
            }
        }

        Result = new ImageAssociationResult
        {
            FolderPath = folderPath,
            WriteToFile = writeToFile,
            Remappings = remappings
        };

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(false);
    }
}
