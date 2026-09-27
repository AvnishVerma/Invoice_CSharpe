using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Defines the XAML structure for global update and notification publishing.</summary>
public sealed partial class PublisherConsoleFormView : UserControl
{
    public PublisherConsoleFormView()
    {
        InitializeComponent();
    }

    public PublisherConsoleFormView(Control release, Control announcement, Control preview, Control publish)
        : this()
    {
        ReleaseHost.Content = release;
        AnnouncementHost.Content = announcement;
        PreviewHost.Content = preview;
        PublishHost.Content = publish;
    }
}
