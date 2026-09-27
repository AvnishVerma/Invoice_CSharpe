using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Renders the live invoice footer totals using an XAML item template.</summary>
public sealed partial class InvoiceTotalsView : UserControl
{
    public ObservableCollection<InvoiceTotalLine> Totals { get; } = [];

    public InvoiceTotalsView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void SetTotals(IEnumerable<InvoiceTotalLine> totals)
    {
        Totals.Clear();
        foreach (var total in totals) Totals.Add(total);
    }
}

public sealed record InvoiceTotalLine(string Label, string Amount, double FontSize);
