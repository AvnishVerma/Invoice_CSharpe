using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts product search, suggestions, and custom-item actions in the invoice editor.</summary>
public sealed partial class InvoiceQuickAddView : UserControl
{
    public InvoiceQuickAddView()
    {
        InitializeComponent();
    }

    public InvoiceQuickAddView(Control search, Control customItem, Control suggestions)
        : this()
    {
        SearchHost.Content = search;
        CustomItemHost.Content = customItem;
        SuggestionsHost.Content = suggestions;
    }
}
