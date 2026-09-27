using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the XAML container for invoice options and additional settings.</summary>
public sealed partial class InvoiceOptionsPanelView : UserControl
{
    public InvoiceOptionsPanelView()
    {
        InitializeComponent();
    }

    public InvoiceOptionsPanelView(Control options)
        : this()
    {
        OptionsHost.Content = options;
    }
}
