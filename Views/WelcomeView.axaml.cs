using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LabelAva.Views;

public partial class WelcomeView : UserControl
{
    public static readonly RoutedEvent OpenTranslationRequestedEvent =
        RoutedEvent.Register<WelcomeView, RoutedEventArgs>(
            nameof(OpenTranslationRequested), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> OpenTranslationRequested
    {
        add => AddHandler(OpenTranslationRequestedEvent, value);
        remove => RemoveHandler(OpenTranslationRequestedEvent, value);
    }

    public WelcomeView()
    {
        InitializeComponent();
    }

    private void OnOpenTranslationFile(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(OpenTranslationRequestedEvent, this));
    }
}
