using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductReportView : UserControl
{
    // Initializes the product report for the AXAML loader.
    public ProductReportView()
    {
        InitializeComponent();
    }

    // Initializes the product report with calculated report data and export commands.
    public ProductReportView(ProductReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Maps product report calculations into chart and table presentation values.
public sealed class ProductReportViewModel
{
    public ProductReportRowViewModel[] Rows { get; }
    public ProductReportRowViewModel[] ChartRows { get; }
    public bool HasRows => Rows.Length > 0;
    public bool HasNoRows => !HasRows;
    public bool HasMissingCosts { get; }
    public string Title => ChartRows.Length == 0 ? "Products / Services by Revenue" : $"Top {ChartRows.Length} Products / Services by Revenue";
    public string MissingCostMessage { get; }
    public string RangeText => HasRows ? $"Rows per page:  10      1 – {Rows.Length} of {Rows.Length}" : "Rows per page:  10      0 items";
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportPdfCommand { get; }

    // Creates ranked rows, normalized bar values, warnings, and export commands.
    public ProductReportViewModel(ProductReportSnapshot report, Func<Task> exportCsv, Func<Task> exportPdf)
    {
        var maximum = Math.Max(1m, report.Products.Select(product => product.Revenue).DefaultIfEmpty(0m).Max());
        Rows = report.Products.Select((product, index) => ProductReportRowViewModel.From(product, index + 1, maximum)).ToArray();
        ChartRows = Rows.Take(5).ToArray();
        HasMissingCosts = report.MissingCostItemCount > 0;
        MissingCostMessage = report.MissingCostItemCount == 1
            ? "1 item sold in this period has no purchase price set — profit/margin is understated for that item until a purchase price is added to the product."
            : $"{report.MissingCostItemCount} items sold in this period have no purchase price set — profit/margin is understated until purchase prices are added to those products.";
        ExportCsvCommand = new AsyncRelayCommand(exportCsv);
        ExportPdfCommand = new AsyncRelayCommand(exportPdf);
    }

    // Formats a monetary value for the product report.
    internal static string Money(decimal value) => $"Rs. {value:N2}";
}

// Provides one formatted product row for the revenue chart and detail table.
public sealed record ProductReportRowViewModel(
    int Index,
    string Name,
    string UnitsSold,
    string Revenue,
    string DiscountGiven,
    string Profit,
    string Margin,
    decimal RevenueValue,
    decimal MaximumRevenue)
{
    // Converts calculated product totals into display-ready values.
    public static ProductReportRowViewModel From(ProductReportProductSnapshot product, int index, decimal maximum) => new(
        index,
        product.Name,
        product.UnitsSold.ToString("0.###"),
        ProductReportViewModel.Money(product.Revenue),
        product.DiscountGiven <= .005m ? "—" : ProductReportViewModel.Money(product.DiscountGiven),
        ProductReportViewModel.Money(product.Profit),
        $"{product.Margin:0.#}%",
        product.Revenue,
        maximum);
}
