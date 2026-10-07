using System.Collections.ObjectModel;
using Avalonia.Controls;
using LedgerNest.Desktop;

namespace LedgerNest.Desktop.Views;

/// <summary>Displays a compact, searchable customer picker for the invoice editor.</summary>
public sealed partial class CustomerChooserView : UserControl
{
    private readonly UiRecord[] customers;
    private readonly Action<UiRecord> select;
    public ObservableCollection<CustomerChooserItem> Items { get; } = [];

    public CustomerChooserView()
        : this(Array.Empty<UiRecord>(), _ => { })
    {
    }

    public CustomerChooserView(IEnumerable<UiRecord> source, Action<UiRecord> onSelect)
    {
        customers = source.OrderBy(customer => customer.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        select = onSelect;
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => Refresh();
        CustomersList.SelectionChanged += (_, _) =>
        {
            if (CustomersList.SelectedItem is CustomerChooserItem item)
            {
                CustomersList.SelectedItem = null;
                select(item.Customer);
            }
        };
        Refresh();
    }

    private void Refresh()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        Items.Clear();
        foreach (var customer in customers.Where(customer => customer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || customer["Business Name"].Contains(query, StringComparison.OrdinalIgnoreCase)
            || customer["Phone"].Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            var detail = new[] { customer["Business Name"], customer["Phone"] }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            Items.Add(new CustomerChooserItem(customer, customer.Name, string.Join("  •  ", detail)));
        }
    }
}

public sealed record CustomerChooserItem(UiRecord Customer, string Name, string Details)
{
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1].ToUpperInvariant();
}
