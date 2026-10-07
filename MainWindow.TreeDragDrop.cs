using System;
using Avalonia.Controls;
using Avalonia.Input;
using LabelAva.Views;

namespace LabelAva;

/// <summary>
/// MainWindow 的「TreeDragDrop」部分。
///
/// 拖拽状态机本身已经搬进 <see cref="TreeDragDropController"/>，这里只保留 XAML 事件入口
/// （MainWindow.axaml 的 TreeDataTemplate 上挂着 PointerPressed/Moved/Released）。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>拖拽控制器；在 InitializeAsync 里、ViewModel 就绪之后创建。</summary>
    private void InitializeTreeDragDrop()
    {
        var treeView = this.FindControl<TreeView>("ImageTreeView")
            ?? throw new InvalidOperationException("找不到 ImageTreeView");

        _treeDragDrop = new TreeDragDropController(
            this, treeView, Navigation, Document, CanvasWorkspace, RequireEditMode);
    }

    private void OnTreeViewItemPointerPressed(object? sender, PointerPressedEventArgs e)
        => _treeDragDrop?.OnPointerPressed(sender, e);

    private void OnTreeViewItemPointerMoved(object? sender, PointerEventArgs e)
        => _treeDragDrop?.OnPointerMoved(sender, e);

    private void OnTreeViewItemPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _treeDragDrop?.OnPointerReleased(sender, e);
}
