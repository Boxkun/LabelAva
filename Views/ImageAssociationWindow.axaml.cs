using Avalonia;
using Avalonia.Animation;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Layout;
using Avalonia.Threading;
using LabelAva.Models;
using LabelAva.Services;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace LabelAva.Views;

public partial class ImageAssociationWindow : Window
{
    private static readonly Avalonia.Media.IBrush ErrorBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xF4, 0x43, 0x36));
    private static readonly Avalonia.Media.IBrush BannerInfoFlash = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x21, 0x96, 0xF3));
    private static readonly Avalonia.Media.IBrush BannerErrorFlash = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xF4, 0x43, 0x36));

    // 横幅浅色模式颜色
    private static readonly Avalonia.Media.IBrush BannerInfoTextLight = Avalonia.Media.Brush.Parse("#1976D2");
    private static readonly Avalonia.Media.IBrush BannerInfoBtnLight = Avalonia.Media.Brush.Parse("#1976D2");
    private static readonly Avalonia.Media.IBrush BannerInfoBorderLight = Avalonia.Media.Brush.Parse("#1976D2");
    private static readonly Avalonia.Media.IBrush BannerErrorTextLight = Avalonia.Media.Brush.Parse("#C62828");
    private static readonly Avalonia.Media.IBrush BannerErrorBorderLight = Avalonia.Media.Brush.Parse("#C62828");
    private static readonly Avalonia.Media.IBrush BannerErrorBtnLight = Avalonia.Media.Brush.Parse("#C62828");

    // 横幅暗色模式颜色
    private static readonly Avalonia.Media.IBrush BannerInfoTextDark = Avalonia.Media.Brush.Parse("#4ea0f3");  
    private static readonly Avalonia.Media.IBrush BannerInfoBtnDark = Avalonia.Media.Brush.Parse("#1976D2");
    private static readonly Avalonia.Media.IBrush BannerInfoBorderDark = Avalonia.Media.Brush.Parse("#4ea0f3");
    private static readonly Avalonia.Media.IBrush BannerErrorTextDark = Avalonia.Media.Brush.Parse("#eb4d4d");
    private static readonly Avalonia.Media.IBrush BannerErrorBorderDark = Avalonia.Media.Brush.Parse("#eb4d4d");
    private static readonly Avalonia.Media.IBrush BannerErrorBtnDark = Avalonia.Media.Brush.Parse("#C62828");

    private readonly ImageValidationService _validationService = new();
    private ObservableCollection<ImageAssociationItem> _items = new();
    private string _imageFolderPath = string.Empty;
    private List<MatchEntry>? _matchEntries;
    private ListBoxDragReorderHelper<ImageAssociationItem>? _dragHelper;
    private List<string> _removedImageNames = new();
    private Dictionary<string, string> _addedImages = new();
    private int _flashVersion;

    private sealed record MatchEntry(
        string ImageName,
        string FilePath,
        bool HasExtensionMismatch,
        bool HasFormatError,
        string? ActualExtension
    );

    private sealed record DetailItem(
        string ImageName,
        string FileName,
        string Status,
        Avalonia.Media.IBrush StatusForeground
    );

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

        // 创建拖拽排序辅助
        _dragHelper = new ListBoxDragReorderHelper<ImageAssociationItem>(
            AssociationList, _items, item => item.ImageName);

        // TextBox 始终为空，Placeholder 始终显示 _imageFolderPath
        FolderPathBox.PlaceholderText = imageFolderPath;

        foreach (var item in _items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        UpdateStatusSummary();
        CheckAutoMatch();
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageAssociationItem.NewPath) && sender is ImageAssociationItem item)
        {
            RevalidateItem(item);
            UpdateStatusSummary();
        }
    }

    private bool _isPathInvalid;

    private void UpdateStatusSummary()
    {
        if (_isPathInvalid)
        {
            StatusSummary.Text = "路径不存在";
            StatusSummary.Foreground = ErrorBrush;
            return;
        }

        var missingCount = _items.Count(i => i.Status == ImageValidationStatus.Missing);
        if (missingCount > 0)
        {
            StatusSummary.Text = $"{missingCount} 张图片存在问题";
            StatusSummary.Foreground = ErrorBrush;
        }
        else
        {
            StatusSummary.Text = "";
        }
    }

    /// <param name="folderPath">要验证的路径；为 null 时使用已提交的 _imageFolderPath</param>
    private void RevalidateAllItems(string? folderPath = null)
    {
        var path = folderPath ?? _imageFolderPath;
        if (string.IsNullOrEmpty(path)) return;

        foreach (var item in _items)
        {
            if (string.IsNullOrEmpty(item.NewPath))
            {
                var (status, statusText) = _validationService.ValidateSingleWithText(path, item.ImageName);
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
            var newPath = folders[0].Path.LocalPath;
            _imageFolderPath = newPath;
            FolderPathBox.PlaceholderText = newPath;
            FolderPathBox.Text = "";
            _isPathInvalid = false;
            _addedImages.Clear();
            _removedImageNames.Clear();
            SaveApplyButton.IsEnabled = true;
            ApplyOnlyButton.IsEnabled = true;
            AutoMatchBanner.Opacity = 0;
            _matchEntries = null;
            AutoMatchBanner.IsVisible = false;
            RevalidateAllItems();
            CheckAutoMatch();
        }
    }

    private void OnFolderPathTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = FolderPathBox.Text?.Trim();
        AutoMatchBanner.Opacity = 0;
        _matchEntries = null;
        AutoMatchBanner.IsVisible = false;

        if (string.IsNullOrEmpty(text))
        {
            // 清空 → 回退到已提交路径
            _isPathInvalid = false;
            SaveApplyButton.IsEnabled = true;
            ApplyOnlyButton.IsEnabled = true;
            RevalidateAllItems();
            CheckAutoMatch();
        }
        else if (Directory.Exists(text))
        {
            // 有效 → 预览验证
            _isPathInvalid = false;
            SaveApplyButton.IsEnabled = true;
            ApplyOnlyButton.IsEnabled = true;
            RevalidateAllItems(text);
            CheckAutoMatch();
        }
        else
        {
            // 非法 → 全部标 Missing，禁用按钮
            _isPathInvalid = true;
            SaveApplyButton.IsEnabled = false;
            ApplyOnlyButton.IsEnabled = false;
            foreach (var item in _items)
            {
                item.Status = ImageValidationStatus.Missing;
                item.StatusText = "✗ 缺失";
            }
            UpdateStatusSummary();
        }
    }

    private async void OnSelectFile(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not ImageAssociationItem item) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var imageFilter = new[]
        {
            new FilePickerFileType("图片文件") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp"} },
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

    private async void OnDeleteImage(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not ImageAssociationItem item) return;

        var dialog = new Dialogs.DeleteImageConfirmDialog(item.ImageName, item.LabelCount);
        await dialog.ShowDialog(this);
        if (!dialog.IsConfirmed) return;

        _removedImageNames.Add(item.ImageName);
        _items.Remove(item);
        UpdateStatusSummary();
    }

    private async void OnAddImage(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var imageFilter = new[]
        {
            new FilePickerFileType("图片文件") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp"} },
            new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
        };

        var startFolder = !string.IsNullOrEmpty(_imageFolderPath) && Directory.Exists(_imageFolderPath)
            ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(_imageFolderPath)
            : null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要添加的图片",
            AllowMultiple = true,
            FileTypeFilter = imageFilter,
            SuggestedStartLocation = startFolder
        });

        if (files.Count == 0) return;

        foreach (var file in files)
        {
            var sourcePath = file.Path.LocalPath;
            var fileName = Path.GetFileName(sourcePath);

            // 跳过列表中已存在的图片
            if (_items.Any(i => i.ImageName == fileName))
                continue;

            var destPath = Path.Combine(_imageFolderPath, fileName);

            // 增量校验：仅对新增图片做格式检测
            var (isConsistent, actualExt) = ImageValidationService.CheckFormatConsistency(sourcePath);
            var status = isConsistent ? ImageValidationStatus.OK : ImageValidationStatus.FormatMismatch;
            var statusText = isConsistent
                ? "✓ 正常"
                : $"⚠ 格式不符 ({actualExt})";

            // 复制到项目路径
            try
            {
                if (!string.Equals(sourcePath, destPath, StringComparison.OrdinalIgnoreCase))
                    File.Copy(sourcePath, destPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OnAddImage] File.Copy FAILED: {ex.Message}");
                continue;
            }

            var newItem = new ImageAssociationItem
            {
                ImageName = fileName,
                Status = status,
                StatusText = statusText,
                NewPath = destPath,
                LabelCount = 0,
            };
            newItem.PropertyChanged += OnItemPropertyChanged;
            _items.Add(newItem);

            _addedImages[fileName] = destPath;
        }

        UpdateStatusSummary();
    }

    private void RevalidateItem(ImageAssociationItem item)
    {
        if (!string.IsNullOrEmpty(item.NewPath))
        {
            var (status, statusText) = ImageValidationService.ValidateFullPath(item.NewPath);
            item.Status = status;
            item.StatusText = statusText;
        }
        else
        {
            var (status, statusText) = _validationService.ValidateSingleWithText(_imageFolderPath, item.ImageName);
            item.Status = status;
            item.StatusText = statusText;
        }
    }

    private bool _lastChosenWriteToFile = true;

    /// <summary>确认时提交 TextBox 中的有效路径到 _imageFolderPath 和水印</summary>
    private void CommitFolderPath()
    {
        var text = FolderPathBox.Text?.Trim();
        if (!string.IsNullOrEmpty(text) && Directory.Exists(text) &&
            !string.Equals(_imageFolderPath, text, StringComparison.OrdinalIgnoreCase))
        {
            _imageFolderPath = text;
            FolderPathBox.PlaceholderText = text;
            _addedImages.Clear();
            _removedImageNames.Clear();
        }
    }

    private void OnSaveApply(object? sender, RoutedEventArgs e)
    {
        _lastChosenWriteToFile = true;
        CommitFolderPath();

        var missingItems = _items.Where(i => i.Status == ImageValidationStatus.Missing && string.IsNullOrEmpty(i.NewPath)).ToList();
        if (missingItems.Count > 0)
        {
            ShowWarningDialog(missingItems.Count);
            return;
        }

        ConfirmInternal(_imageFolderPath, writeToFile: true);
    }

    private void OnApplyOnly(object? sender, RoutedEventArgs e)
    {
        _lastChosenWriteToFile = false;
        CommitFolderPath();

        var missingItems = _items.Where(i => i.Status == ImageValidationStatus.Missing && string.IsNullOrEmpty(i.NewPath)).ToList();
        if (missingItems.Count > 0)
        {
            ShowWarningDialog(missingItems.Count);
            return;
        }

        ConfirmInternal(_imageFolderPath, writeToFile: false);
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
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None },
            Background = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush")
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
            var folderPath = _imageFolderPath;
            ConfirmInternal(folderPath, _lastChosenWriteToFile);
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

        // 从 _items 的当前顺序投影最终图片顺序
        var orderedNames = _items.Select(i => i.ImageName).ToList();

        Result = new ImageAssociationResult
        {
            FolderPath = folderPath,
            WriteToFile = writeToFile,
            Remappings = remappings,
            OrderedImageNames = orderedNames,
            AddedImages = _addedImages.Count > 0 ? _addedImages : null,
            RemovedImageNames = _removedImageNames.Count > 0 ? _removedImageNames : null,
        };

        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(false);
    }

    // ========================
    // 拖拽排序事件处理器（委托给 ListBoxDragReorderHelper）
    // ========================

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
        => _dragHelper?.OnPointerPressed(sender, e);

    private void OnItemPointerMoved(object? sender, PointerEventArgs e)
        => _dragHelper?.OnPointerMoved(sender, e);

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _dragHelper?.OnPointerReleased(sender, e);

    private async void FlashBanner(Avalonia.Media.IBrush flashColor, Avalonia.Media.IBrush softBg, Avalonia.Media.IBrush softBorder)
    {
        var version = ++_flashVersion;

        AutoMatchBanner.Transitions = new Transitions
        {
            new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.Zero },
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.Zero },
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromSeconds(0.3) }
        };
        AutoMatchBanner.Background = flashColor;
        AutoMatchBanner.BorderBrush = flashColor;
        AutoMatchBanner.Opacity = 1;
        AutoMatchBanner.IsVisible = true;

        await Task.Delay(80);

        if (version != _flashVersion) return;

        AutoMatchBanner.Transitions = new Transitions
        {
            new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromSeconds(0.8) },
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromSeconds(0.8) },
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromSeconds(0.3) }
        };
        AutoMatchBanner.Background = softBg;
        AutoMatchBanner.BorderBrush = softBorder;
    }

    private void CheckAutoMatch()
    {
        if (string.IsNullOrEmpty(_imageFolderPath))
            return;

        _matchEntries = new List<MatchEntry>();

        var missingItems = _items
            .Where(i => i.Status == ImageValidationStatus.Missing && string.IsNullOrEmpty(i.NewPath))
            .ToList();

        var altMatches = _validationService.FindAlternateExtensionMatches(
            _imageFolderPath, missingItems.Select(i => i.ImageName));

        foreach (var kvp in altMatches)
        {
            Debug.WriteLine($"[CheckAutoMatch Phase1] ImageName={kvp.Key} FilePath={Path.GetFileName(kvp.Value)}");
            _matchEntries.Add(new MatchEntry(
                kvp.Key,
                kvp.Value,
                HasExtensionMismatch: true,
                HasFormatError: false,
                ActualExtension: null));
        }

        if (_matchEntries.Count > 0)
        {
            var srcExts = _matchEntries
                .Select(e => Path.GetExtension(e.ImageName).ToLowerInvariant())
                .Distinct()
                .ToArray();
            var dstExts = _matchEntries
                .Select(e => Path.GetExtension(e.FilePath).ToLowerInvariant())
                .Distinct()
                .ToArray();
            var extText = $"{string.Join(" / ", srcExts)} → {string.Join(" / ", dstExts)}";

            var sb = new StringBuilder();
            sb.AppendLine($"找不到翻译文件内指定的图片（{string.Join(" / ", srcExts)}）。在当前文件夹下发现 {_matchEntries.Count} 张同名的其他格式图片（{string.Join(" / ", dstExts)}）。");

            // if (_matchEntries.Count <= 3)
            // {
            //     foreach (var entry in _matchEntries)
            //     {
            //         var fileName = Path.GetFileName(entry.FilePath);
            //         sb.AppendLine($"{entry.ImageName} → {fileName}");
            //     }
            // }

            sb.Append("要使用找到的图片替代吗？");

            AutoMatchBannerText.Text = sb.ToString();
            ActionButton.Content = "填入";
            ViewDetailsButton.IsVisible = false;

            var softBg = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush") ?? Avalonia.Media.Brushes.White;

            var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var softBorder = isDark ? BannerInfoBorderDark : BannerInfoBorderLight;

            AutoMatchBannerText.Foreground = isDark ? BannerInfoTextDark : BannerInfoTextLight;
            ActionButton.Background = isDark ? BannerInfoBtnDark : BannerInfoBtnLight;
            ActionButton.Foreground = Avalonia.Media.Brushes.White;

            if (IsLoaded)
            {
                FlashBanner(BannerInfoFlash, softBg, softBorder);
            }
            else
            {
                AutoMatchBanner.Background = softBg;
                AutoMatchBanner.BorderBrush = softBorder;
                AutoMatchBanner.Opacity = 1;
                AutoMatchBanner.IsVisible = true;
                EventHandler handler = null!;
                handler = (_, _) =>
                {
                    Opened -= handler;
                    Dispatcher.UIThread.InvokeAsync(() => FlashBanner(BannerInfoFlash, softBg, softBorder),
                        Avalonia.Threading.DispatcherPriority.Loaded);
                };
                Opened += handler;
            }
            Debug.WriteLine($"[CheckAutoMatch] Case1: {_matchEntries.Count} entries, banner shown");
            return;
        }

        foreach (var item in _items)
        {
            var resolvedPath = !string.IsNullOrEmpty(item.NewPath)
                ? item.NewPath
                : Path.Combine(_imageFolderPath, item.ImageName);

            if (!File.Exists(resolvedPath))
                continue;

            var (isConsistent, actualExt) = ImageValidationService.CheckFormatConsistency(resolvedPath);
            if (!isConsistent)
            {
                Debug.WriteLine($"[CheckAutoMatch Phase2] ImageName={item.ImageName} resolvedPath={Path.GetFileName(resolvedPath)} actualExt={actualExt}");
                _matchEntries.Add(new MatchEntry(
                    item.ImageName,
                    resolvedPath,
                    HasExtensionMismatch: false,
                    HasFormatError: true,
                    actualExt));

                // 同步更新列表项状态，确保 Banner 与列表语义一致
                item.Status = ImageValidationStatus.FormatMismatch;
                item.StatusText = $"⚠ 格式不符 ({actualExt})";
            }
        }

        if (_matchEntries.Count == 0)
            return;

        var sb2 = new StringBuilder();
        sb2.AppendLine($"发现 {_matchEntries.Count} 张图片的实际格式与扩展名不符，可能导致 Photoshop 加载异常。");

        // if (_matchEntries.Count <= 3)
        // {
        //     foreach (var entry in _matchEntries)
        //     {
        //         var fileName = Path.GetFileName(entry.FilePath);
        //         sb2.AppendLine($"{entry.ImageName} → {fileName} ⚠ {entry.ActualExtension}");
        //     }
        // }

        sb2.Append("要修正扩展名错误，并且更新翻译文件吗？");

        AutoMatchBannerText.Text = sb2.ToString();
        ActionButton.Content = "修正";
        ViewDetailsButton.IsVisible = true;

        var errorBg = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush") ?? Avalonia.Media.Brushes.White;

        var isDarkErr = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var errorBorder = isDarkErr ? BannerErrorBorderDark : BannerErrorBorderLight;

        AutoMatchBannerText.Foreground = isDarkErr ? BannerErrorTextDark : BannerErrorTextLight;
        ActionButton.Background = isDarkErr ? BannerErrorBtnDark : BannerErrorBtnLight;
        ActionButton.Foreground = Avalonia.Media.Brushes.White;

        if (IsLoaded)
        {
            FlashBanner(BannerErrorFlash, errorBg, errorBorder);
        }
        else
        {
            AutoMatchBanner.Background = errorBg;
            AutoMatchBanner.BorderBrush = errorBorder;
            AutoMatchBanner.Opacity = 1;
            AutoMatchBanner.IsVisible = true;
            EventHandler handler = null!;
            handler = (_, _) =>
            {
                Opened -= handler;
                Dispatcher.UIThread.InvokeAsync(
                    () => FlashBanner(BannerErrorFlash, errorBg, errorBorder),
                    Avalonia.Threading.DispatcherPriority.Loaded);
            };
            Opened += handler;
        }
        Debug.WriteLine($"[CheckAutoMatch] Case2: {_matchEntries.Count} entries, banner shown");
    }

    private void OnAutoFill(object? sender, RoutedEventArgs e)
    {
        if (_matchEntries == null)
            return;

        var isPhase1 = _matchEntries.Any(en => en.HasExtensionMismatch);

        foreach (var entry in _matchEntries)
        {
            var item = _items.FirstOrDefault(i => i.ImageName == entry.ImageName);
            if (item == null)
                continue;

            Debug.WriteLine($"[OnAutoFill] ImageName={entry.ImageName} FilePath={Path.GetFileName(entry.FilePath)} HasFormatError={entry.HasFormatError} HasExtMismatch={entry.HasExtensionMismatch} ActualExt={entry.ActualExtension} item.NewPath={item.NewPath}");

            if (entry.HasFormatError && entry.ActualExtension != null)
            {
                var correctPath = Path.ChangeExtension(
                    entry.FilePath,
                    entry.ActualExtension.TrimStart('.'));

                Debug.WriteLine($"[OnAutoFill] HasFormatError branch: correctPath={Path.GetFileName(correctPath)} exists={File.Exists(correctPath)}");

                if (!File.Exists(correctPath))
                {
                    try
                    {
                        File.Move(entry.FilePath, correctPath);
                        Debug.WriteLine($"[OnAutoFill] File.Move OK: {Path.GetFileName(entry.FilePath)} -> {Path.GetFileName(correctPath)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OnAutoFill] File.Move FAILED: {ex.Message}");
                        continue;
                    }
                }

                item.NewPath = correctPath;
            }
            else if (entry.HasExtensionMismatch)
            {
                if (!string.IsNullOrEmpty(item.NewPath))
                    continue;
                Debug.WriteLine($"[OnAutoFill] ExtMismatch branch: set NewPath={entry.FilePath}");
                item.NewPath = entry.FilePath;
            }

            RevalidateItem(item);
            Debug.WriteLine($"[OnAutoFill] After Revalidate: item.NewPath={item.NewPath} Status={item.Status} StatusText={item.StatusText}");
        }

        AutoMatchBanner.Opacity = 0;
        _matchEntries = null;
        AutoMatchBanner.IsVisible = false;

        if (isPhase1)
        {
            CheckAutoMatch();
            if (AutoMatchBanner.IsVisible)
                _lastChosenWriteToFile = true;
            UpdateStatusSummary();
        }
        else
        {
            _lastChosenWriteToFile = true;
            UpdateStatusSummary();
            ConfirmInternal(_imageFolderPath, writeToFile: true);
            return;
        }
    }

    private void OnDismissBanner(object? sender, RoutedEventArgs e)
    {
        AutoMatchBanner.Opacity = 0;
        _matchEntries = null;
        AutoMatchBanner.IsVisible = false;
    }

    private async void OnViewDetails(object? sender, RoutedEventArgs e)
    {
        if (_matchEntries == null || _matchEntries.Count == 0)
            return;

        var dialog = new Window
        {
            Title = "扩展名匹配详情",
            Width = 520,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInTaskbar = false,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None },
            Background = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundAltHighBrush")
        };

        var detailNormalFg = Services.ThemeHelper.GetBrush("SystemControlForegroundBaseHighBrush") ?? Avalonia.Media.Brushes.Black;
        var detailBorderBrush = Services.ThemeHelper.GetBrush("SystemControlForegroundChromeMediumBrush") ?? Avalonia.Media.Brush.Parse("#DDD");
        var detailHeaderBg = Services.ThemeHelper.GetBrush("SystemControlPageBackgroundChromeLowBrush") ?? Avalonia.Media.Brush.Parse("#F5F5F5");

        var items = _matchEntries.Select(entry =>
        {
            var fileName = Path.GetFileName(entry.FilePath);
            string status;
            Avalonia.Media.IBrush fg;
            if (entry.HasFormatError)
            {
                status = $"{entry.ActualExtension}";
                fg = ErrorBrush;
            }
            else
            {
                status = entry.HasExtensionMismatch ? $"{entry.ActualExtension}" : "-";
                fg = detailNormalFg;
            }
            return new DetailItem(entry.ImageName, fileName, status, fg);
        }).ToList();

        // 行模板（与主列表样式一致），直接使用 item 赋值避免反射绑定
        var itemTemplate = new FuncDataTemplate<object>((data, _) =>
        {
            var item = (DetailItem)data;

            var border = new Border
            {
                BorderBrush = detailBorderBrush,
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
                Padding = new Avalonia.Thickness(6, 8)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1.2, GridUnitType.Star))
                },
                VerticalAlignment = VerticalAlignment.Center
            };

            var imageNameText = new TextBlock
            {
                Text = item.ImageName,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                FontFamily = Avalonia.Media.FontFamily.Parse("Sarasa Mono SC"),
                Margin = new Avalonia.Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(imageNameText, 0);
            grid.Children.Add(imageNameText);

            var fileNameText = new TextBlock
            {
                Text = item.FileName,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                FontFamily = Avalonia.Media.FontFamily.Parse("Sarasa Mono SC"),
            };
            Grid.SetColumn(fileNameText, 1);
            grid.Children.Add(fileNameText);

            var statusText = new TextBlock
            {
                Text = item.Status,
                Foreground = item.StatusForeground,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                FontFamily = Avalonia.Media.FontFamily.Parse("Sarasa Mono SC")
            };
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);

            border.Child = grid;
            return border;
        });

        // 表头行
        var headerBorder = new Border
        {
            BorderBrush = detailBorderBrush,
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Padding = new Avalonia.Thickness(6, 8),
            Background = detailHeaderBg
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.2, GridUnitType.Star))
            },
            VerticalAlignment = VerticalAlignment.Center
        };

        var headerImageName = new TextBlock
        {
            Text = "目标图片",
            FontSize = 13,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(16, 0, 0, 0)
        };
        Grid.SetColumn(headerImageName, 0);
        headerGrid.Children.Add(headerImageName);

        var headerFileName = new TextBlock
        {
            Text = "找到的图片",
            FontSize = 13,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerFileName, 1);
        headerGrid.Children.Add(headerFileName);

        var headerStatus = new TextBlock
        {
            Text = "推测实际格式",
            FontSize = 13,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerStatus, 2);
        headerGrid.Children.Add(headerStatus);

        headerBorder.Child = headerGrid;

        // 列表
        var itemsControl = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = itemTemplate,
            Margin = new Avalonia.Thickness(12, 4, 12, 4)
        };

        // 组合
        var listPanel = new DockPanel();
        listPanel.Children.Add(headerBorder);
        DockPanel.SetDock(headerBorder, Dock.Top);
        listPanel.Children.Add(itemsControl);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = listPanel
        };

        var outerBorder = new Border
        {
            BorderBrush = detailBorderBrush,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Child = scrollViewer
        };

        var closeButton = new Button
        {
            Content = "关闭",
            Width = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };
        closeButton.Click += (s, args) => dialog.Close();

        var rootPanel = new DockPanel { Margin = new Avalonia.Thickness(16) };
        rootPanel.Children.Add(closeButton);
        DockPanel.SetDock(closeButton, Dock.Bottom);
        rootPanel.Children.Add(outerBorder);

        dialog.Content = rootPanel;

        await dialog.ShowDialog(this);
    }
}
