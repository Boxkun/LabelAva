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
/// MainWindow 的「FileDrop」部分。
/// 从资源管理器拖文件到窗口打开。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null || files.Length == 0)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var hasTxtFile = files.Any(f =>
            f.Path.IsFile &&
            f.Path.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        e.DragEffects = hasTxtFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        var txtFile = files?.FirstOrDefault(f =>
            f.Path.IsFile &&
            f.Path.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        if (txtFile == null)
            return;

        var filePath = txtFile.Path.LocalPath;
        await Document.OpenTranslationFileAsync(filePath);
    }
}
