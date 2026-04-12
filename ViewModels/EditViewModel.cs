using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabelAva.ViewModels;

/// <summary>
/// 编辑模式 ViewModel：仅管理编辑模式 UI 状态（IsEditMode、CurrentGroupIndex）。
/// 标签操作（AddLabel/DeleteLabel/MoveLabel/ChangeGroup/ReorderLabels）已迁入 CanvasWorkspaceViewModel。
/// </summary>
public partial class EditViewModel : ObservableObject
{
    private readonly StatusBarViewModel _statusBar;

    // ========================
    // 状态属性
    // ========================

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _canToggleEditMode;

    [ObservableProperty]
    private int _currentGroupIndex;

    // ========================
    // 派生属性（供 XAML 绑定）
    // ========================

    /// <summary>编辑面板是否可见</summary>
    public bool IsEditPanelVisible => IsEditMode;

    /// <summary>编辑模式按钮文本</summary>
    public string EditModeButtonText => IsEditMode ? "编辑模式" : "查看模式";

    /// <summary>分组按钮是否可见</summary>
    public bool AreGroupButtonsVisible => IsEditMode;

    // ========================
    // 命令
    // ========================

    [RelayCommand(CanExecute = nameof(CanToggleEditMode))]
    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        // 副作用（状态栏通知、EditModeChanged 事件）由 OnIsEditModeChanged 处理
    }

    [RelayCommand]
    private void SwitchGroup(int groupIndex)
    {
        CurrentGroupIndex = groupIndex;
        GroupChanged?.Invoke(this, EventArgs.Empty);
        _statusBar.UpdateStatus(
            $"当前分组：{(groupIndex == 0 ? "框内" : "框外")}，点击图片添加标记",
            StatusBarViewModel.StatusType.Info);
    }

    // ========================
    // 构造函数
    // ========================

    public EditViewModel(StatusBarViewModel statusBar)
    {
        _statusBar = statusBar;
    }

    // ========================
    // 内部方法
    // ========================

    partial void OnIsEditModeChanged(bool value)
    {
        UpdateDerivedProperties();
        ToggleEditModeCommand.NotifyCanExecuteChanged();

        // 仅在文档已打开时触发副作用（避免初始化时误触发）
        if (CanToggleEditMode)
        {
            EditModeChanged?.Invoke(this, EventArgs.Empty);

            _statusBar.UpdateStatus(
                value ? "已进入编辑模式：左键点击图片以新建标签，中键/右键拖动平移"
                       : "已退出编辑模式",
                value ? StatusBarViewModel.StatusType.Success
                       : StatusBarViewModel.StatusType.Info);
        }
    }

    partial void OnCanToggleEditModeChanged(bool value)
    {
        ToggleEditModeCommand.NotifyCanExecuteChanged();
    }

    private void UpdateDerivedProperties()
    {
        OnPropertyChanged(nameof(IsEditPanelVisible));
        OnPropertyChanged(nameof(EditModeButtonText));
        OnPropertyChanged(nameof(AreGroupButtonsVisible));
    }

    // ========================
    // 事件
    // ========================

    /// <summary>编辑模式变更事件（通知 MainWindow 更新 UI 细节）</summary>
    public event EventHandler? EditModeChanged;

    /// <summary>分组变更事件（通知 MainWindow 更新 RadioButton 状态）</summary>
    public event EventHandler? GroupChanged;
}
