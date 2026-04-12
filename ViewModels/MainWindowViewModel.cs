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

    [ObservableProperty]
    private DocumentViewModel _document = null!; // 由 MainWindow 构造时注入

    [ObservableProperty]
    private NavigationViewModel _navigation = null!; // 由 MainWindow 构造时注入

    [ObservableProperty]
    private CanvasWorkspaceViewModel _canvasWorkspace = null!; // 由 MainWindow 构造时注入（替代原 ImageViewportViewModel）
}
