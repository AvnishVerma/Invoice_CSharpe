using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceEditorShellView : UserControl
{
    public InvoiceEditorShellView()
    {
        InitializeComponent();
    }

    public InvoiceEditorShellView(Control header, Control workspace, Control footer)
    {
        InitializeComponent();
        HeaderHost.Content = header;
        WorkspaceHost.Content = workspace;
        FooterHost.Content = footer;
        SizeChanged += (_, e) => FooterFrame.Padding = new Thickness(e.NewSize.Width < 700 ? 8 : 64, 10);
    }
}
