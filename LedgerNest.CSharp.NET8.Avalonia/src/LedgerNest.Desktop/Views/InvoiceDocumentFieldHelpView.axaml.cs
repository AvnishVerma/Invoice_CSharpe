using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceDocumentFieldHelpView : UserControl
{
    public InvoiceDocumentFieldHelpView() => InitializeComponent();
    public InvoiceDocumentFieldHelpView(bool metadata) : this() => DataContext = new InvoiceDocumentFieldHelpModel(metadata
        ? "Product metadata columns print on supported non-thermal invoice templates."
        : "Custom fields print on all invoice templates, including thermal receipts.");
}

public sealed record InvoiceDocumentFieldHelpModel(string Text);
