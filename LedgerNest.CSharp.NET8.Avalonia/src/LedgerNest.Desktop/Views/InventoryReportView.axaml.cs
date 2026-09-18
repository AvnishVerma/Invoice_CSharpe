using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class InventoryReportView : UserControl
{
    // Initializes the inventory report for the AXAML loader.
    public InventoryReportView()
    {
        InitializeComponent();
    }

    // Initializes the inventory report with valuation data and export actions.
    public InventoryReportView(InventoryReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides inventory KPI, paging, warning, and export state to the AXAML view.
public sealed class InventoryReportViewModel : INotifyPropertyChanged
{
    private const int PageSize = 25;
    private readonly InventoryReportRowViewModel[] allRows;
    private int pageIndex;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string InventoryValue { get; }
    public string PotentialSaleValue { get; }
    public string ProfitLockedInStock { get; }
    public string TotalUnits { get; }
    public int ProductsTracked { get; }
    public int ExcludedItemCount { get; }
    public bool HasExcludedItems => ExcludedItemCount > 0;
    public string ExclusionMessage => $"{ExcludedItemCount} {(ExcludedItemCount == 1 ? "item is" : "items are")} excluded from inventory value — {(ExcludedItemCount == 1 ? "it's" : "they're")} a service or {(ExcludedItemCount == 1 ? "has" : "have")} unlimited stock tracking on.";
    public InventoryReportRowViewModel[] VisibleRows => allRows.Skip(pageIndex * PageSize).Take(PageSize).ToArray();
    public bool HasNoRows => allRows.Length == 0;
    public int PageCount => Math.Max(1, (int)Math.Ceiling(allRows.Length / (decimal)PageSize));
    public bool CanPrevious => pageIndex > 0;
    public bool CanNext => pageIndex + 1 < PageCount;
    public string PageText => $"Page {pageIndex + 1} of {PageCount}";
    public string RangeText
    {
        get
        {
            if (allRows.Length == 0) return "Rows per page:  25      0 items";
            var start = pageIndex * PageSize + 1;
            var end = Math.Min(allRows.Length, start + PageSize - 1);
            return $"Rows per page:  25      {start} – {end} of {allRows.Length}";
        }
    }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportPdfCommand { get; }

    // Creates formatted inventory values, paged rows, and report actions.
    public InventoryReportViewModel(InventoryReportSnapshot report, Func<Task> exportCsv, Func<Task> exportPdf)
    {
        InventoryValue = Money(report.InventoryValue);
        PotentialSaleValue = Money(report.PotentialSaleValue);
        ProfitLockedInStock = Money(report.ProfitLockedInStock);
        TotalUnits = report.TotalUnits.ToString("N0");
        ProductsTracked = report.ProductsTracked;
        ExcludedItemCount = report.ExcludedItemCount;
        allRows = report.Products.Select(InventoryReportRowViewModel.From).ToArray();
        PreviousPageCommand = new RelayCommand(() => ChangePage(-1));
        NextPageCommand = new RelayCommand(() => ChangePage(1));
        ExportCsvCommand = new AsyncRelayCommand(exportCsv);
        ExportPdfCommand = new AsyncRelayCommand(exportPdf);
    }

    // Moves to an available inventory page and refreshes paging bindings.
    private void ChangePage(int offset)
    {
        var target = Math.Clamp(pageIndex + offset, 0, PageCount - 1);
        if (target == pageIndex) return;
        pageIndex = target;
        OnPropertyChanged(nameof(VisibleRows));
        OnPropertyChanged(nameof(CanPrevious));
        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(RangeText));
    }

    // Formats an inventory currency amount consistently.
    private static string Money(decimal value) => $"Rs. {value:N2}";

    // Raises a property-change notification for one presentation property.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

// Provides one formatted product valuation row.
public sealed record InventoryReportRowViewModel(string Name, string Stock, string PurchasePrice, string StockValue, string SaleValue)
{
    // Converts one product valuation into display-ready values.
    public static InventoryReportRowViewModel From(InventoryProductSnapshot product) => new(
        product.Name,
        product.Stock.ToString("0.###"),
        $"Rs. {product.PurchasePrice:N2}",
        $"Rs. {product.StockValue:N2}",
        $"Rs. {product.SaleValue:N2}");
}
