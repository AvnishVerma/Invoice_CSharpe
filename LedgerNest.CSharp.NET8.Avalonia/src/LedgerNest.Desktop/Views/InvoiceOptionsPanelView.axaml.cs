using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the XAML container for invoice options and additional settings.</summary>
public sealed partial class InvoiceOptionsPanelView : UserControl
{
    private bool isAttached;
    private InvoiceOptionsViewModel? attachedModel;

    public InvoiceOptionsPanelView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => { isAttached = true; UpdateAttachment(); };
        DetachedFromVisualTree += (_, _) => { isAttached = false; UpdateAttachment(); };
        DataContextChanged += (_, _) => UpdateAttachment();
    }

    private void UpdateAttachment()
    {
        var next = isAttached ? DataContext as InvoiceOptionsViewModel : null;
        if (ReferenceEquals(next, attachedModel)) return;
        attachedModel?.Detach();
        attachedModel = next;
        attachedModel?.Attach();
    }

    public InvoiceOptionsPanelView(MainWindowViewModel workspace)
        : this()
    {
        DataContext = new InvoiceOptionsViewModel(workspace);
    }
}
