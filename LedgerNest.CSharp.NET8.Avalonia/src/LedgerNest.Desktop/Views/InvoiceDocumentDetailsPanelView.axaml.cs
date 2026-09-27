using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts the live document details fields in the invoice editor.</summary>
public sealed partial class InvoiceDocumentDetailsPanelView : UserControl
{
    public InvoiceDocumentDetailsPanelView()
    {
        InitializeComponent();
    }

    public InvoiceDocumentDetailsPanelView(Control details)
        : this()
    {
        DetailsHost.Content = details;
    }
}
