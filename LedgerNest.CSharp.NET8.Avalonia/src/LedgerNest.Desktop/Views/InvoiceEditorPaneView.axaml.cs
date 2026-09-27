using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the consistent two-section column layout for the invoice editor workspace.</summary>
public sealed partial class InvoiceEditorPaneView : UserControl
{
    public InvoiceEditorPaneView()
    {
        InitializeComponent();
    }

    public InvoiceEditorPaneView(Control top, Control bottom)
        : this()
    {
        TopHost.Content = top;
        BottomHost.Content = bottom;
    }
}
