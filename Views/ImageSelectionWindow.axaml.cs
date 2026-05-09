using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
/// 自然排序：数字段按数值比较，非数字段按字符比较。避免文件系统 inode 序影响列表顺序。
/// </summary>
internal static class NaturalSort
{
    public static int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int ix = 0, iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                while (ix < x.Length && x[ix] == '0') ix++;
                while (iy < y.Length && y[iy] == '0') iy++;

                int startX = ix, startY = iy;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;

                int lenX = ix - startX;
                int lenY = iy - startY;
                if (lenX != lenY) return lenX.CompareTo(lenY);

                for (int k = 0; k < lenX; k++)
                {
                    int cmp = x[startX + k].CompareTo(y[startY + k]);
                    if (cmp != 0) return cmp;
                }
            }
            else
            {
                int cmp = x[ix].CompareTo(y[iy]);
                if (cmp != 0) return cmp;
                ix++;
                iy++;
            }
        }

        return (x.Length - ix).CompareTo(y.Length - iy);
    }
}

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
    
    public ImageSelectionWindow()
    {
        InitializeComponent();
    }

    public ImageSelectionWindow(IEnumerable<string> imageFiles, string defaultFileName)
    {
        InitializeComponent();
        
        // 创建图片选择项列表（按文件名自然排序，避免文件系统 inode 序）
        var items = imageFiles
            .OrderBy(f => Path.GetFileName(f), Comparer<string>.Create(NaturalSort.Compare))
            .Select(f => new ImageSelectionItem
            {
                FileName = Path.GetFileName(f),
                FilePath = f,
                IsSelected = true
            }).ToList();
        
        ImageListControl.ItemsSource = items;
        
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

    /// <summary>
    /// 加载图片并进行重采样（缩放到最大尺寸限制）
    /// </summary>
    private Bitmap? LoadScaledBitmap(string filePath, int maxWidth, int maxHeight)
    {
        using var stream = File.OpenRead(filePath);
        var bitmap = new Bitmap(stream);
        
        // 如果图片小于限制尺寸，直接返回原图
        if (bitmap.Size.Width <= maxWidth && bitmap.Size.Height <= maxHeight)
        {
            return bitmap;
        }
        
        // 计算缩放比例
        double scaleX = (double)maxWidth / bitmap.Size.Width;
        double scaleY = (double)maxHeight / bitmap.Size.Height;
        double scale = Math.Min(scaleX, scaleY);
        
        int newWidth = (int)(bitmap.Size.Width * scale);
        int newHeight = (int)(bitmap.Size.Height * scale);
        
        // 创建缩放后的位图
        var scaledBitmap = bitmap.CreateScaledBitmap(new PixelSize(newWidth, newHeight), BitmapInterpolationMode.HighQuality);
        bitmap.Dispose();
        
        return scaledBitmap;
    }
}
