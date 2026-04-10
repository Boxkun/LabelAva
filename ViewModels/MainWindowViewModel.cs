using CommunityToolkit.Mvvm.ComponentModel;
namespace LabelAva.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private StatusBarViewModel _statusBar = new();

    [ObservableProperty]
    private HistoryViewModel _history = null!; // 由 MainWindow 构造时注入

    [ObservableProperty]
    private EditViewModel _edit = null!; // 由 MainWindow 构造时注入

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
}
