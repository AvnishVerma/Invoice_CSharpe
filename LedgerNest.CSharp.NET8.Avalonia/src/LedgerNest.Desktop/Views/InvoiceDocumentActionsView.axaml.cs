using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts saved-document action buttons in the invoice editor footer.</summary>
public sealed partial class InvoiceDocumentActionsView : UserControl
{
    public InvoiceDocumentActionsView()
    {
        InitializeComponent();
    }

    public void Add(Control action) => ActionsHost.Children.Add(action);
}
