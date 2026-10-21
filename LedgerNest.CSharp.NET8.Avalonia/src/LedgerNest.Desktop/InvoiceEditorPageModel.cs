using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public sealed partial class InvoiceEditorPageModel : ObservableObject
{
    private readonly Action<UiRecord> selectProduct;
    private bool completed;
    public MainWindowViewModel Workspace { get; }
    public InvoiceCustomerPanelViewModel Customer { get; }
    public InvoiceDocumentDetailsViewModel Details { get; }
    public InvoiceOptionsViewModel Options { get; }
    public InvoiceLinesPresentation LineItems { get; } = new();
    public InvoiceTotalsPresentation TotalsModel { get; } = new();
    public ObservableCollection<InvoiceProductSuggestion> Suggestions { get; } = [];
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private bool showSuggestions;
    [ObservableProperty] private bool isNarrow;
    public string CustomItemCaption => IsNarrow ? "Custom item" : "Custom item (Ctrl+M)";
    partial void OnIsNarrowChanged(bool value) => OnPropertyChanged(nameof(CustomItemCaption));
    [ObservableProperty] private InvoiceProductSuggestion? selectedSuggestion;
    public string Title => $"{(Workspace.IsEditingDocument ? "Edit" : "Create New")} {Workspace.InvoiceDetails[0].Value}";
    public string DateText => DateTime.TryParse(Workspace.InvoiceDetails[1].Value, out var date) ? date.ToString("dd/MM/yyyy") : Workspace.InvoiceDetails[1].Value;
    public string NumberText => "Invoice Number: #" + Workspace.EditorDocumentNumber;
    public string ItemCount => Workspace.Lines.Count + (Workspace.Lines.Count == 1 ? " item" : " items");
    public string SaveCaption => $"{(Workspace.IsEditingDocument ? "Save" : "Create")} {Workspace.InvoiceDetails[0].Value} (Ctrl+S)";
    public bool ShowVoid => Workspace.IsEditingDocument && Workspace.InvoiceDetails[0].Value == "Invoice";
    public IRelayCommand CreateCommand { get; }
    public IRelayCommand CustomItemCommand { get; }
    public IRelayCommand VoidCommand { get; }
    public IRelayCommand ViewCommand { get; }
    public IAsyncRelayCommand PreviewCommand { get; }
    public IAsyncRelayCommand DownloadCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }
    public event Action? FocusSearchRequested;
    public void RequestSearchFocus() => FocusSearchRequested?.Invoke();

    public InvoiceEditorPageModel(MainWindowViewModel workspace, Action selectCustomer, Action<UiRecord> selectProduct,
        Action customItem, Action saved, Action voidInvoice, Action view, Func<Task> preview, Func<Task> download, Func<Task> print, Action<FormField> expand, Action? createCustomer = null)
    {
        Workspace = workspace; this.selectProduct = selectProduct;
        Customer = new(workspace, selectCustomer, expand, createCustomer); Details = new(workspace); Options = new(workspace, expand);
        CreateCommand = new RelayCommand(() => { if (!completed && workspace.SaveInvoice()) { completed = true; CreateCommand!.NotifyCanExecuteChanged(); saved(); } }, () => !completed && workspace.Lines.Count > 0);
        CustomItemCommand = new RelayCommand(customItem);
        VoidCommand = new RelayCommand(voidInvoice, () => workspace.CanVoidCurrentInvoice);
        ViewCommand = new RelayCommand(view, () => workspace.LastSavedDocument != null);
        PreviewCommand = new AsyncRelayCommand(preview, () => workspace.LastSavedDocument != null);
        DownloadCommand = new AsyncRelayCommand(download, () => workspace.LastSavedDocument != null);
        PrintCommand = new AsyncRelayCommand(print, () => workspace.LastSavedDocument != null);
        Refresh();
    }

    public void Attach() { Workspace.InvoiceChanged -= Refresh; Workspace.InvoiceChanged += Refresh; Refresh(); }
    public void Detach() => Workspace.InvoiceChanged -= Refresh;
    partial void OnSearchTextChanged(string value)
    {
        Suggestions.Clear();
        foreach (var product in Workspace.SearchProducts(value.Trim())) Suggestions.Add(new(product, Workspace));
        ShowSuggestions = value.Trim().Length > 0;
    }
    partial void OnSelectedSuggestionChanged(InvoiceProductSuggestion? value)
    {
        if (value == null) return;
        SelectedSuggestion = null; SearchText = ""; ShowSuggestions = false; selectProduct(value.Record);
    }
    public void AcceptSearch()
    {
        var query = SearchText.Trim(); if (query.Length == 0) return;
        var exact = Workspace.ExactProductCodeMatches(query);
        var matches = exact.Length > 0 ? exact : Workspace.SearchProducts(query);
        if (matches.Length == 1) { SearchText = ""; ShowSuggestions = false; selectProduct(matches[0]); }
    }
    private void Refresh()
    {
        foreach (var name in new[] { nameof(Title), nameof(DateText), nameof(NumberText), nameof(ItemCount), nameof(SaveCaption), nameof(ShowVoid) }) OnPropertyChanged(name);
        LineItems.Set(Workspace);
        var t = Workspace.Totals;
        TotalsModel.Totals.Clear();
        TotalsModel.Totals.Add(new("Subtotal", CurrencyDisplay.Format(t.Subtotal, currency: Workspace.EditorCurrency), 13));
        if (t.ItemDiscount + t.InvoiceDiscount != 0) TotalsModel.Totals.Add(new("Discount", CurrencyDisplay.Format(t.ItemDiscount + t.InvoiceDiscount, currency: Workspace.EditorCurrency), 13));
        TotalsModel.Totals.Add(new("Tax", CurrencyDisplay.Format(t.Tax, currency: Workspace.EditorCurrency), 13));
        if (t.AdditionalCosts != 0) TotalsModel.Totals.Add(new("Charges & adjustments", CurrencyDisplay.Format(t.AdditionalCosts, currency: Workspace.EditorCurrency), 13));
        TotalsModel.Totals.Add(new("Total", CurrencyDisplay.Format(t.Total, currency: Workspace.EditorCurrency), 18));
        CreateCommand.NotifyCanExecuteChanged(); VoidCommand.NotifyCanExecuteChanged(); ViewCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged(); DownloadCommand.NotifyCanExecuteChanged(); PrintCommand.NotifyCanExecuteChanged();
    }
}

public sealed class InvoiceLinesPresentation : ObservableObject
{
    public ObservableCollection<InvoiceLineItemModel> Lines { get; } = [];
    public bool HasLines => Lines.Count > 0;
    public bool HasNoLines => !HasLines;
    public void Set(MainWindowViewModel workspace)
    {
        var existing = Lines.Select(row => row.Line).ToArray();
        if (!existing.SequenceEqual(workspace.Lines))
        {
            Lines.Clear();
            foreach (var line in workspace.Lines) Lines.Add(new(line, workspace.InvoiceSetting("Show Product / Service Tag").IsChecked, new RelayCommand(() => workspace.Lines.Remove(line))));
        }
        OnPropertyChanged(nameof(HasLines)); OnPropertyChanged(nameof(HasNoLines));
    }
}
public sealed class InvoiceTotalsPresentation { public ObservableCollection<InvoiceTotalLine> Totals { get; } = []; }

public sealed class InvoiceProductSuggestion
{
    public UiRecord Record { get; }
    public string Name => Record.Name;
    public string Metadata { get; }
    public bool IsOutOfStock { get; }
    public InvoiceProductSuggestion(UiRecord record, MainWindowViewModel workspace)
    {
        Record = record;
        var unlimited = bool.TryParse(record["Unlimited stock"], out var flag) && flag;
        var inStock = !workspace.ProductFieldVisible("Stock") || !decimal.TryParse(record["Stock"], out var stock) || stock > 0 || unlimited;
        IsOutOfStock = !inStock;
        var parts = new List<string> { CurrencyDisplay.Format(decimal.TryParse(record["Sale Price"], out var amount) ? amount : 0, currency: workspace.EditorCurrency) };
        if (workspace.ProductFieldVisible("Stock")) parts.Add("Stock: " + (unlimited ? "∞" : record["Stock"]));
        if (workspace.InvoiceSetting("Show GST fields").IsChecked && workspace.ProductFieldVisible("HSN/SAC")) parts.Add("HSN " + record["HSN/SAC"]);
        if (workspace.ProductFieldVisible("Storage Location") && record["Storage Location"].Length > 0) parts.Add(record["Storage Location"]);
        Metadata = string.Join(" • ", parts);
    }
}
