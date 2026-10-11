using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceEditorShellView : UserControl
{
    public InvoiceEditorShellView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as InvoiceEditorPageModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as InvoiceEditorPageModel)?.Detach();
    }
    public InvoiceEditorShellView(InvoiceEditorPageModel model) : this() => DataContext = model;
}
