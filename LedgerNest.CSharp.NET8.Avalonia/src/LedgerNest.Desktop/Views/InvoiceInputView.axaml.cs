using Avalonia.Controls;
using Avalonia.VisualTree;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceInputView : UserControl
{
    private InvoiceInputViewModel? attached;
    public InvoiceInputView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => AttachModel();
        DetachedFromVisualTree += (_, _) => { attached?.Detach(); attached = null; };
        DataContextChanged += (_, _) => { if (this.IsAttachedToVisualTree()) AttachModel(); };
    }
    private void AttachModel() { attached?.Detach(); attached = DataContext as InvoiceInputViewModel; attached?.Attach(); }
}
