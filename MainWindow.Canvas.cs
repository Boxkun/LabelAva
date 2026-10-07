using Avalonia;
using Avalonia.Controls;
using LabelAva.Models;
using LabelAva.Commands;
using LabelAva.ViewModels;


namespace LabelAva;

/// <summary>
/// MainWindow 的「Canvas」部分。
/// 画布与标签交互：标记选中/新增、画布事件回调、适配变换、图像加载与呈现。
/// <para>由 code-behind 机械拆分而来：成员体逐字节未改，只换了文件位置。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 根据索引选中文本节点并在树视图中聚焦
    /// </summary>
    private void SelectLabelByIndex(int labelIndex)
    {
        // 委托到 NavigationViewModel 执行核心选中逻辑
        Navigation.SelectLabelByIndex(labelIndex);
        
        // UI 层补充操作
        if (Navigation.SelectedItem != null)
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
            StatusBar.UpdateStatus($"已选中标注 #{labelIndex}", StatusBarViewModel.StatusType.Info);
        }
    }
    
    /// <summary>
    /// 在指定坐标新建标签（如果该位置没有现有标签）
    /// </summary>
    private void AddNewLabel(double imageX, double imageY)
    {
        if (CanvasControl.CurrentImage == null || Document.TranslationData == null || string.IsNullOrEmpty(CanvasControl.CurrentImagePath) || Navigation.CurrentTreeItem == null)
            return;

        string imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
        
        // 确保字典中有该图片的数据列表（TryGetCurrentLabels 不适用，因为此处需要创建新列表）
        if (!Document.TranslationData.ImageLabels.TryGetValue(imageName, out var labels))
        {
            labels = new List<LabelItem>();
            Document.TranslationData.ImageLabels[imageName] = labels;
        }

        // 计算新的编号 (TextIndex)，取当前最大值 + 1
        int nextIndex = labels.Any() ? labels.Max(l => l.TextIndex) + 1 : 1;

        // 计算归一化坐标 (0.0 ~ 1.0)
        double normX = imageX / CanvasControl.CurrentImage!.Size.Width;
        double normY = imageY / CanvasControl.CurrentImage!.Size.Height;

        // 创建底层数据
        var newLabel = new LabelItem
        {
            ImageName = imageName,
            TextIndex = nextIndex,
            X = normX,
            Y = normY,
            GroupIndex = Edit.CurrentGroupIndex + 1, // 从1开始：0→1, 1→2
            Text = ""
        };
        
        // 创建并执行 AddLabelCommand（命令会自动刷新 UI）
        CanvasWorkspace.AddLabel(labels, newLabel, nextIndex);

        // 注意：UI 刷新由 HistoryManager.HistoryChanged 事件处理
    }
    
    
    private void OnCanvasAddLabelRequested(object? sender, (double pixelX, double pixelY) coords)
    {
        // 检查是否在编辑模式
        if (!Edit.IsEditMode)
            return;
        
        // 检查点击位置是否已有现有标签
        if (Document.TranslationData == null || string.IsNullOrEmpty(CanvasControl.CurrentImagePath))
            return;
        
        var labels = TryGetCurrentLabels();
        if (labels == null)
        {
            // 当前图片尚无标签，直接添加
            AddNewLabel(coords.pixelX, coords.pixelY);
            return;
        }
        
        // 检查是否命中现有标签
        int? hitIndex = CanvasWorkspace.FindLabelAtPosition(coords.pixelX, coords.pixelY, 
            CanvasControl.CurrentImage?.Size.Width ?? 0, 
            CanvasControl.CurrentImage?.Size.Height ?? 0, 
            labels);
        
        if (hitIndex.HasValue)
        {
            _isSelectionFromCanvas = true;
            SelectLabelByIndex(hitIndex.Value);
        }
        else
        {
            _isSelectionFromCanvas = true;
            AddNewLabel(coords.pixelX, coords.pixelY);
        }
    }
    
    /// <summary>
    /// 画布标签拖拽结束后的位置变更处理
    /// </summary>
    private void OnCanvasLabelMoved(object? sender, (int textIndex, double oldNormX, double oldNormY, double newNormX, double newNormY) args)
    {
        if (TryGetCurrentLabels() is not { } labels)
            return;
        
        var label = labels.FirstOrDefault(l => l.TextIndex == args.textIndex);
        if (label != null)
        {
            _isSelectionFromCanvas = true;
            CanvasWorkspace.MoveLabel(label, args.oldNormX, args.oldNormY, args.newNormX, args.newNormY);
        }
    }

    /// <summary>
    /// 画布标注按下删除（由 AnnotationCanvas 的 EditMode+Delete 动作触发）
    /// </summary>
    private void OnCanvasLabelDeleteRequested(object? sender, int labelIndex)
    {
        // 不经过选中，避免触发 SelectionChanged → 聚焦 → 视口闪烁
        if (!RequireEditMode()) return;
        if (TryGetCurrentLabels() is not { } labels) return;

        var labelToRemove = labels.FirstOrDefault(l => l.TextIndex == labelIndex);
        if (labelToRemove == null) return;

        var command = new DeleteLabelCommand(labels, labelToRemove);
        ViewModel.History.ExecuteCommand(command);
    }

    /// <summary>
    /// 画布标注按下弹出右键菜单（由 AnnotationCanvas 的 ContextMenu 动作触发）
    /// </summary>
    private void OnCanvasLabelContextMenuRequested(object? sender, (int labelIndex, PixelPoint screenPos) args)
    {
        _isSelectionFromCanvas = true;
        SelectLabelByIndex(args.labelIndex);

        if (Navigation.SelectedItem is not TranslationTreeItem)
            return;

        // 关闭前一个菜单，防止复数菜单并存
        _canvasContextMenu?.Close();
        _canvasContextMenu = null;

        var copyItem = new MenuItem { Header = "复制文本" };
        copyItem.Click += (s, e) =>
        {
            if (Navigation.SelectedItem is TranslationTreeItem item)
            {
                CopyToClipboard(item.Text);
                StatusBar.UpdateStatus($"已复制: {SanitizeStatusText(item.Text)}", StatusBarViewModel.StatusType.Info);
            }
        };

        var deleteItem = new MenuItem { Header = "删除此标记" };
        deleteItem.Click += (s, e) => DeleteSelectedLabel();

        var toggleItem = new MenuItem { Header = "切换分组" };
        toggleItem.Click += (s, e) => OnToggleGroup(s, e);

        _canvasContextMenu = new ContextMenu();
        _canvasContextMenu.Items.Add(copyItem);
        _canvasContextMenu.Items.Add(deleteItem);
        _canvasContextMenu.Items.Add(new Separator());
        _canvasContextMenu.Items.Add(toggleItem);
        _canvasContextMenu.Open(CanvasControl);
    }

    /// <summary>
    /// 计算适应容器的初始变换（Fit模式）—— 委托给 CanvasWorkspaceViewModel
    /// </summary>
    private void CalculateFitTransform()
    {
        CanvasControl.CalculateFitTransform();
    }
    
    /// <summary>
    /// 保存当前图片的 fit 缩放比例到对应的树视图项
    /// </summary>
    private void SaveCurrentFitScale(double fitScale)
    {
        if (CanvasControl.CurrentImagePath == null || Navigation.TreeItems.Count == 0) return;
        
        string imageName = Path.GetFileName(CanvasControl.CurrentImagePath);
        var item = Navigation.FindTreeItemByImageName(imageName);
        if (item != null)
        {
            item.FitScale = fitScale;
            Navigation.CurrentTreeItem = item;
        }
    }
    
    // ==================== 图片加载 ====================

    /// <summary>
    /// 更新标注：根据当前图片的标注数据，在画布上显示编号
    /// </summary>
    private void UpdateLabels()
    {
        // 没有图片或翻译数据时返回
        if (CanvasControl.CurrentImage == null || TryGetCurrentLabels() is not { } labels)
            return;

        // 通过 CanvasControl 更新标注显示，并高亮当前选中项
        int? highlightIndex = (Navigation.SelectedItem is TranslationTreeItem selectedTranslation) 
            ? selectedTranslation.Index 
            : null;
        
        CanvasControl.UpdateLabels(labels, CanvasControl.CurrentImage.Size.Width, CanvasControl.CurrentImage.Size.Height, highlightIndex);
    }
    
    /// <summary>
    /// 加载当前图片
    /// </summary>
    private void LoadCurrentImage()
    {
        if (Navigation.ImageNames.Count == 0 || string.IsNullOrEmpty(Navigation.ImageFolderPath))
            return;
        
        if (Navigation.CurrentImageIndex < 0 || Navigation.CurrentImageIndex >= Navigation.ImageNames.Count)
            return;
        
        var imageName = Navigation.ImageNames[Navigation.CurrentImageIndex];
        var imagePath = ResolveImagePath(imageName);
        
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            LoadImage(imagePath);
        }
        else
        {
            CanvasControl.ShowErrorPlaceholder(imageName);
            StatusBar.UpdateStatus($"找不到指定的文件: {imageName}", StatusBarViewModel.StatusType.Warn);
        }
    }
    
    private string ResolveImagePath(string imageName)
    {
        if (Navigation.ImagePathMapping.TryGetValue(imageName, out var mappedPath))
            return mappedPath;

        return Path.Combine(Navigation.ImageFolderPath!, imageName);
    }
    
    private void LoadImage(string imagePath)
    {
        try
        {
            // 通过 CanvasControl 加载图片
            CanvasControl.LoadImage(imagePath);
            
            // 找到当前图片对应的树视图项
            string imageName = Path.GetFileName(imagePath);
            var treeItem = Navigation.FindTreeItemByImageName(imageName);
            if (treeItem != null)
            {
                Navigation.CurrentTreeItem = treeItem;
            }
            
            // 通知 CanvasWorkspace 图片尺寸
            CanvasWorkspace.UpdateImageSize(new Size(CanvasControl.CurrentImage!.Size.Width, CanvasControl.CurrentImage!.Size.Height));
            
            // 首次加载时延迟计算适应容器的初始变换，等待布局完成
            if (!CanvasControl.IsFirstImageLoaded)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // 等待布局完成
                    
                    // 在 UI 线程上执行
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        CalculateFitTransform();
                        CanvasControl.MarkFirstImageLoaded();
                        
                        // 更新标注显示
                        UpdateLabels();
                    });
                });
            }
            else
            {
                // 非首次加载直接应用已有的变换
                CanvasControl.ApplyTransform();
                StatusBar.UpdateZoom(CanvasWorkspace.ZoomPercent);
                // 更新标注显示
                UpdateLabels();
            }
            
            // 更新状态栏显示当前图片信息
            if (Navigation.ImageNames.Count > 0)
            {
                StatusBar.UpdateStatus($"[{Navigation.CurrentImageIndex + 1}/{Navigation.ImageNames.Count}] {Path.GetFileName(imagePath)}");
            }
            else
            {
                StatusBar.UpdateStatus($"已加载图片: {Path.GetFileName(imagePath)}", StatusBarViewModel.StatusType.Info);
            }
        }
        catch (Exception ex)
        {
            StatusBar.UpdateStatus($"加载图片失败: {ex.Message}", StatusBarViewModel.StatusType.Error);
        }
    }
    
    // ==================== 辅助方法 ====================
    
}
