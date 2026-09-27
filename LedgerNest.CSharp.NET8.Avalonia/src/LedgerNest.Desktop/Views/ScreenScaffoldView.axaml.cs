using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the shared XAML page frame used by settings and management content.</summary>
public sealed partial class ScreenScaffoldView : UserControl
{
    public ScreenScaffoldView()
    {
        InitializeComponent();
    }

    public ScreenScaffoldView(Control header, Control body)
        : this()
    {
        HeaderHost.Content = header;
        BodyHost.Content = body;
    }
}
