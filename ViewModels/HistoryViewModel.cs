using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelAva.Commands;
using LabelAva.Services;

namespace LabelAva.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryManager _historyManager;
    private readonly Action _commitCurrentEdit;
    private readonly StatusBarViewModel _statusBar;

    // ========================
    // 状态属性
    // ========================

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string _undoHeader = "撤销(_U)";

    [ObservableProperty]
    private string _redoHeader = "重做(_R)";

    // ========================
    // 命令
    // ========================

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _commitCurrentEdit();
        _historyManager.Undo();
        _statusBar.UpdateStatus("已撤销", StatusBarViewModel.StatusType.Info);
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _commitCurrentEdit();
        _historyManager.Redo();
        _statusBar.UpdateStatus("已重做", StatusBarViewModel.StatusType.Info);
    }

    // ========================
    // 构造函数
    // ========================

    public HistoryViewModel(HistoryManager historyManager, Action commitCurrentEdit, StatusBarViewModel statusBar)
    {
        _historyManager = historyManager;
        _commitCurrentEdit = commitCurrentEdit;
        _statusBar = statusBar;
        _historyManager.HistoryChanged += OnHistoryChanged;
    }

    // ========================
    // 内部方法
    // ========================

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        CanUndo = _historyManager.CanUndo;
        CanRedo = _historyManager.CanRedo;

        var recentUndo = _historyManager.GetRecentUndoDescriptions(1);
        var recentRedo = _historyManager.GetRecentRedoDescriptions(1);

        UndoHeader = (CanUndo && recentUndo.Count > 0)
            ? $"撤销 {recentUndo[0]}"
            : "撤销(_U)";
        RedoHeader = (CanRedo && recentRedo.Count > 0)
            ? $"重做 {recentRedo[0]}"
            : "重做(_R)";

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        // 通知外部历史已变化（用于脏标记等）
        HistoryStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 执行命令并记录到历史（供外部业务操作调用）
    /// </summary>
    public void ExecuteCommand(IUndoableCommand command)
    {
        _historyManager.ExecuteCommand(command);
    }

    /// <summary>
    /// 清空历史记录
    /// </summary>
    public void Clear()
    {
        _historyManager.Clear();
    }

    /// <summary>
    /// 历史状态变化事件（用于通知 MainWindow 设置脏标记和重建视图）
    /// </summary>
    public event EventHandler? HistoryStateChanged;
}
