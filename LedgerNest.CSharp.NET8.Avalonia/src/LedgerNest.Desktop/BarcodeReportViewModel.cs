using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public sealed partial class BarcodeProductRow : ObservableObject
{
    public UiRecord Product { get; init; } = null!;
    public string Name => Product.Name;
    public string ProductCode => Product["Product ID"];
    public string Barcode => Product["Barcode"];
    public string PrintableCode => string.IsNullOrWhiteSpace(Barcode) ? ProductCode : Barcode;
    public decimal Price => decimal.TryParse(Product["Sale Price"], out var value) ? value : 0;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private int quantity = 1;
}

public sealed partial class BarcodeReportViewModel : ObservableObject
{
    private readonly Func<BarcodeLabelData[], Task> export;
    private readonly BarcodeProductRow[] allRows;
    public ObservableCollection<BarcodeProductRow> VisibleRows { get; } = [];
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    public int SelectedLabelCount => allRows.Where(row => row.IsSelected).Sum(row => Math.Max(0, row.Quantity));

    public BarcodeReportViewModel(IEnumerable<UiRecord> products, Func<BarcodeLabelData[], Task> export)
    {
        this.export = export;
        allRows = products.Where(product => product["Type"] != "Service").OrderBy(product => product.Name)
            .Select(product => new BarcodeProductRow { Product = product }).ToArray();
        foreach (var row in allRows) row.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectedLabelCount));
        RefreshVisible();
    }

    partial void OnSearchTextChanged(string value) => RefreshVisible();
    private void RefreshVisible()
    {
        VisibleRows.Clear();
        var query = SearchText.Trim();
        foreach (var row in allRows.Where(row => query.Length == 0 || new[] { row.Name, row.ProductCode, row.Barcode }
                     .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase)))) VisibleRows.Add(row);
    }

    [RelayCommand] private void SelectVisible() { foreach (var row in VisibleRows) row.IsSelected = true; OnPropertyChanged(nameof(SelectedLabelCount)); }
    [RelayCommand] private void ClearSelection() { foreach (var row in allRows) row.IsSelected = false; OnPropertyChanged(nameof(SelectedLabelCount)); }

    public BarcodeLabelData[] BuildLabels() => BarcodeRules.Expand(allRows.Where(row => row.IsSelected)
        .Select(row => (new BarcodeLabelData(row.Name, row.PrintableCode, row.Price), row.Quantity)));

    [RelayCommand]
    private async Task ExportAsync()
    {
        Error = ""; IsBusy = true;
        try
        {
            var labels = BuildLabels();
            if (labels.Length == 0) { Error = "Select at least one product."; return; }
            await export(labels);
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
