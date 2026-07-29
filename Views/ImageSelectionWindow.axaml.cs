using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LabelAva.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LabelAva.Views;

/// <summary>
/// 图片选择项数据模型
/// </summary>
public class ImageSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public partial class ImageSelectionWindow : Window
{
    /// <summary>
    /// 用户选择的图片文件路径列表
    /// </summary>
    public List<string> SelectedImagePaths { get; private set; } = new();
    
    /// <summary>
    /// 父窗口（用于模态对话框定位）
    /// </summary>
    public new Window? Owner { get; set; }
    
    /// <summary>
    /// 用户输入的文件名（不含扩展名）
    /// </summary>
    public string FileName { get; private set; } = string.Empty;

    private ObservableCollection<ImageSelectionItem>? _items;
    private ListBoxDragReorderHelper<ImageSelectionItem>? _dragHelper;
    
    public ImageSelectionWindow()
    {
        InitializeComponent();
    }

    public ImageSelectionWindow(IEnumerable<string> imageFiles, string defaultFileName)
    {
        InitializeComponent();
        
        // 创建图片选择项列表（按文件名自然排序，避免文件系统 inode 序）
        _items = new ObservableCollection<ImageSelectionItem>(imageFiles
            .OrderBy(f => Path.GetFileName(f), Comparer<string>.Create(NaturalSort.Compare))
            .Select(f => new ImageSelectionItem
            {
                FileName = Path.GetFileName(f),
                FilePath = f,
                IsSelected = true
            }));
        
        ImageListControl.ItemsSource = _items;

        // 创建拖拽排序辅助
        _dragHelper = new ListBoxDragReorderHelper<ImageSelectionItem>(
            ImageListControl, _items, item => item.FileName);

        // 直接赋值
        FileNameTextBox.Text = defaultFileName;
        
        UpdateConfirmButtonState();
    }

    private void OnSelectAll(object? sender, RoutedEventArgs e)
    {
        if (ImageListControl.ItemsSource is IEnumerable<ImageSelectionItem> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = true;
            }
        }
    }

    private void OnDeselectAll(object? sender, RoutedEventArgs e)
    {
        if (ImageListControl.ItemsSource is IEnumerable<ImageSelectionItem> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = false;
            }
        }
    }

    private async void OnConfirm(object? sender, RoutedEventArgs e)
    {
        // 收集所有选中的图片路径
        if (ImageListControl.ItemsSource is IEnumerable<ImageSelectionItem> items)
        {
            SelectedImagePaths = items
                .Where(i => i.IsSelected)
                .Select(i => i.FilePath)
                .ToList();
        }
        
        // 获取用户输入的文件名
        FileName = FileNameTextBox.Text?.Trim() ?? string.Empty;
        
        // 关闭对话框并返回结果
        Close(true);
    }
    
    private void OnFileNameTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateConfirmButtonState();
    }
    
    private void UpdateConfirmButtonState()
    {
        if (ConfirmButton == null) return;
        var hasFileName = !string.IsNullOrWhiteSpace(FileNameTextBox?.Text);
        ConfirmButton.IsEnabled = hasFileName;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        SelectedImagePaths.Clear();
        Close(false);
    }

    private async void OnImageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_dragHelper is { IsDragging: true }) return;

        if (ImageListControl.SelectedItem is ImageSelectionItem selectedItem)
        {
            try
            {
                if (File.Exists(selectedItem.FilePath))
                {
                    // 异步加载图片以避免阻塞 UI 线程
                    var bitmap = await Task.Run(() => LoadScaledBitmap(selectedItem.FilePath, 800, 600));
                    PreviewImage.Source = bitmap;
                }
                else
                {
                    PreviewImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                PreviewImage.Source = null;
                System.Diagnostics.Debug.WriteLine($"Failed to load image preview: {ex.Message}");
            }
        }
        else
        {
            PreviewImage.Source = null;
        }
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
        => _dragHelper?.OnPointerPressed(sender, e);

    private void OnItemPointerMoved(object? sender, PointerEventArgs e)
        => _dragHelper?.OnPointerMoved(sender, e);

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _dragHelper?.OnPointerReleased(sender, e);

    /// <summary>
    /// 加载图片并进行重采样（缩放到最大尺寸限制）
    /// </summary>
    private static Bitmap? LoadScaledBitmap(string filePath, int maxWidth, int maxHeight)
    {
        return ImageLoader.LoadScaled(filePath, maxWidth, maxHeight);
    }
}
