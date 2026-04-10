using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelAva.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LabelAva.ViewModels;

public partial class NavigationViewModel : ObservableObject
{
    private readonly StatusBarViewModel _statusBar;

    // ========================
    // 状态属性
    // ========================

    /// <summary>树视图数据</summary>
    [ObservableProperty]
    private ObservableCollection<ImageTreeItem> _treeItems = new();

    /// <summary>当前选中的树视图项</summary>
    [ObservableProperty]
    private object? _selectedItem;

    /// <summary>当前图片索引</summary>
    [ObservableProperty]
    private int _currentImageIndex = -1;

    /// <summary>图片文件名列表</summary>
    [ObservableProperty]
    private List<string> _imageNames = new();

    /// <summary>图片文件夹路径</summary>
    [ObservableProperty]
    private string? _imageFolderPath;

    /// <summary>当前图片对应的树视图项</summary>
    [ObservableProperty]
    private ImageTreeItem? _currentTreeItem;

    /// <summary>上一次焦点的根节点（手风琴效果）</summary>
    [ObservableProperty]
    private ImageTreeItem? _lastFocusedRootItem;

    /// <summary>是否有文档打开</summary>
    [ObservableProperty]
    private bool _hasDocument;

    // ========================
    // 派生属性
    // ========================

    /// <summary>当前图片名称</summary>
    public string CurrentImageName =>
        CurrentImageIndex >= 0 && CurrentImageIndex < ImageNames.Count
            ? ImageNames[CurrentImageIndex]
            : string.Empty;

    /// <summary>图片总数</summary>
    public int ImageCount => ImageNames.Count;

    /// <summary>导航是否可用</summary>
    public bool CanNavigate => HasDocument && ImageNames.Count > 0;

    // ========================
    // 命令
    // ========================

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void NavigateUp()
    {
        var visibleItems = GetVisibleItems();
        if (visibleItems.Count == 0 || SelectedItem == null) return;

        int currentIndex = visibleItems.IndexOf(SelectedItem);
        if (currentIndex > 0)
        {
            // 仅设置属性，partial void OnSelectedItemChanged() 会自动触发事件
            SelectedItem = visibleItems[currentIndex - 1];
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void NavigateDown()
    {
        var visibleItems = GetVisibleItems();
        if (visibleItems.Count == 0 || SelectedItem == null) return;

        int currentIndex = visibleItems.IndexOf(SelectedItem);
        if (currentIndex >= 0 && currentIndex < visibleItems.Count - 1)
        {
            // 仅设置属性，partial void OnSelectedItemChanged() 会自动触发事件
            SelectedItem = visibleItems[currentIndex + 1];
        }
    }

    // ========================
    // 公开方法
    // ========================

    /// <summary>从 TranslationData 构建树视图</summary>
    public void BuildTreeView(TranslationData? translationData)
    {
        TreeItems.Clear();

        if (translationData == null) return;

        bool isFirstItem = true;
        foreach (var kvp in translationData.ImageLabels)
        {
            var imageItem = new ImageTreeItem
            {
                ImageName = kvp.Key,
                IsExpanded = isFirstItem
            };
            isFirstItem = false;

            foreach (var label in kvp.Value)
            {
                imageItem.Translations.Add(new TranslationTreeItem
                {
                    Index = label.TextIndex,
                    Text = label.Text,
                    GroupIndex = label.GroupIndex
                });
            }

            TreeItems.Add(imageItem);
        }
    }

    /// <summary>初始化导航状态（文档打开后调用）</summary>
    public void InitializeNavigation(string imageFolderPath, List<string> imageNames)
    {
        ImageFolderPath = imageFolderPath;
        ImageNames = imageNames;
        CurrentImageIndex = imageNames.Count > 0 ? 0 : -1;
        HasDocument = true;
    }

    /// <summary>清除导航状态（文档关闭后调用）</summary>
    public void ClearNavigation()
    {
        TreeItems.Clear();
        ImageNames.Clear();
        ImageFolderPath = null;
        CurrentImageIndex = -1;
        CurrentTreeItem = null;
        LastFocusedRootItem = null;
        SelectedItem = null;
        HasDocument = false;
    }

    /// <summary>根据图片名切换到指定图片，返回是否实际切换</summary>
    public bool TrySwitchToImage(string imageName)
    {
        var index = ImageNames.IndexOf(imageName);
        if (index >= 0 && CurrentImageIndex != index)
        {
            CurrentImageIndex = index;
            UpdateCurrentTreeItem();
            return true;
        }
        return false;
    }

    /// <summary>更新 CurrentTreeItem（图片加载后调用）</summary>
    public void UpdateCurrentTreeItem()
    {
        if (CurrentImageIndex >= 0 && CurrentImageIndex < ImageNames.Count)
        {
            var imageName = ImageNames[CurrentImageIndex];
            CurrentTreeItem = TreeItems.FirstOrDefault(t => t.ImageName == imageName);
        }
        else
        {
            CurrentTreeItem = null;
        }
    }

    /// <summary>执行手风琴展开/收起</summary>
    public void ApplyAccordion(ImageTreeItem targetRootItem)
    {
        foreach (var item in TreeItems)
        {
            item.IsExpanded = (item == targetRootItem);
        }
        LastFocusedRootItem = targetRootItem;
    }

    /// <summary>查找子节点对应的父节点</summary>
    public ImageTreeItem? GetParentImageItem(TranslationTreeItem child)
    {
        return TreeItems.FirstOrDefault(root => root.Translations.Contains(child));
    }

    /// <summary>获取当前可见的树视图项列表（展开的节点包含子项）</summary>
    public List<object> GetVisibleItems()
    {
        var visibleItems = new List<object>();
        foreach (var root in TreeItems)
        {
            visibleItems.Add(root);
            if (root.IsExpanded)
            {
                foreach (var child in root.Translations)
                    visibleItems.Add(child);
            }
        }
        return visibleItems;
    }

    /// <summary>根据索引选中标签</summary>
    public void SelectLabelByIndex(int labelIndex)
    {
        if (CurrentTreeItem == null) return;

        var translationItem = CurrentTreeItem.Translations
            .FirstOrDefault(t => t.Index == labelIndex);

        if (translationItem != null)
        {
            CurrentTreeItem.IsExpanded = true;
            // 仅设置属性，partial void OnSelectedItemChanged() 会自动触发事件
            SelectedItem = translationItem;
        }
    }

    /// <summary>刷新树视图（通知 UI 重新绑定）</summary>
    public void RefreshTreeView()
    {
        OnPropertyChanged(nameof(TreeItems));
    }

    /// <summary>
    /// 判断拖拽是否可以在两个 TranslationTreeItem 之间进行（同图片内）
    /// </summary>
    public bool CanDropItem(TranslationTreeItem sourceItem, TranslationTreeItem targetItem)
    {
        if (sourceItem == null || targetItem == null || sourceItem == targetItem)
            return false;

        var sourceParent = GetParentImageItem(sourceItem);
        var targetParent = GetParentImageItem(targetItem);

        return sourceParent != null && sourceParent == targetParent;
    }

    // ========================
    // 内部方法
    // ========================

    /// <summary>
    /// 当 SelectedItem 由 VM 侧变更时（如键盘导航），触发事件通知 MainWindow 同步 UI
    /// </summary>
    private void OnSelectedItemChangedFromVM()
    {
        SelectedItemChanged?.Invoke(this, EventArgs.Empty);

        // 处理图片切换
        HandleImageSwitchFromSelection();

        // 处理手风琴效果
        HandleAccordionFromSelection();
    }

    /// <summary>根据选中项判断是否需要切换图片</summary>
    private void HandleImageSwitchFromSelection()
    {
        if (SelectedItem is ImageTreeItem rootItem)
        {
            TrySwitchToImage(rootItem.ImageName);
        }
        else if (SelectedItem is TranslationTreeItem childItem)
        {
            var parent = GetParentImageItem(childItem);
            if (parent != null)
            {
                TrySwitchToImage(parent.ImageName);
            }
        }
    }

    /// <summary>根据选中项执行手风琴效果</summary>
    private void HandleAccordionFromSelection()
    {
        ImageTreeItem? targetRoot = null;

        if (SelectedItem is ImageTreeItem rootItem)
        {
            targetRoot = rootItem;
        }
        else if (SelectedItem is TranslationTreeItem childItem)
        {
            targetRoot = GetParentImageItem(childItem);
        }

        if (targetRoot != null)
        {
            ApplyAccordion(targetRoot);
        }
    }

    partial void OnSelectedItemChanged(object? value)
    {
        // 当 SelectedItem 被任何源（热键、命令、代码）修改时，自动触发事件
        // 这确保无论从哪里设置选中项，MainWindow 都能收到通知
        OnSelectedItemChangedFromVM();
    }

    partial void OnCurrentImageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentImageName));
        CurrentImageChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnHasDocumentChanged(bool value)
    {
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateDownCommand.NotifyCanExecuteChanged();
    }

    // ========================
    // 事件
    // ========================

    /// <summary>当前图片变更事件（通知 MainWindow 加载图片、更新画布）</summary>
    public event EventHandler? CurrentImageChanged;

    /// <summary>选中项变更事件（由 VM 侧发起，通知 MainWindow 同步 TreeView.SelectedItem + 高亮 + TextBox）</summary>
    public event EventHandler? SelectedItemChanged;

    // ========================
    // 构造函数
    // ========================

    public NavigationViewModel(StatusBarViewModel statusBar)
    {
        _statusBar = statusBar;
    }
}
