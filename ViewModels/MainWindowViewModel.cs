using CommunityToolkit.Mvvm.ComponentModel;
namespace LabelAva.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private StatusBarViewModel _statusBar = new();

    // ========================
    // 文件菜单
    // ========================

    [ObservableProperty]
    private bool _canSave;

    [ObservableProperty]
    private bool _canSaveAs;

    [ObservableProperty]
    private bool _canCloseTranslation;


    // ========================
    // 编辑菜单
    // ========================

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string _undoHeader = "撤销(_U)";

    [ObservableProperty]
    private string _redoHeader = "重做(_R)";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _canToggleEditMode;


    // ========================
    // 视图菜单
    // ========================

    [ObservableProperty]
    private bool _canZoomIn = true;

    [ObservableProperty]
    private bool _canZoomOut = true;

    [ObservableProperty]
    private bool _canResetZoom = true;


    // ========================
    // 状态更新辅助方法
    // ========================

    public void SetFileState(bool hasDocument)
    {
        CanSave = hasDocument;
        CanSaveAs = hasDocument;
        CanCloseTranslation = hasDocument;
    }

    public void SetUndoRedoState(bool canUndo, bool canRedo, string? undoName, string? redoName)
    {
        CanUndo = canUndo;
        CanRedo = canRedo;

        UndoHeader = undoName ?? "撤销(_U)";
        RedoHeader = redoName ?? "重做(_R)";
    }
}
