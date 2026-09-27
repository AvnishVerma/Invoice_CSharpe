using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Defines the XAML structure for the invoice editor's product and line-item panel.</summary>
public sealed partial class InvoiceItemsPanelView : UserControl
{
    public InvoiceItemsPanelView()
    {
        InitializeComponent();
    }

    public InvoiceItemsPanelView(Control count, Control quickAdd, Control lines)
        : this()
    {
        CountHost.Content = count;
        QuickAddHost.Content = quickAdd;
        LinesHost.Content = lines;
    }
}
