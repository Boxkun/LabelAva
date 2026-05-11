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

    public bool IsTextEditable =>
        Edit != null && Navigation != null && Edit.IsEditMode && Navigation.SelectedTranslationItem != null;

    partial void OnEditChanged(EditViewModel value)
    {
        if (value != null)
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EditViewModel.IsEditMode))
                    OnPropertyChanged(nameof(IsTextEditable));
            };
    }

    partial void OnNavigationChanged(NavigationViewModel value)
    {
        if (value != null)
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavigationViewModel.SelectedTranslationItem))
                    OnPropertyChanged(nameof(IsTextEditable));
            };
    }
}
