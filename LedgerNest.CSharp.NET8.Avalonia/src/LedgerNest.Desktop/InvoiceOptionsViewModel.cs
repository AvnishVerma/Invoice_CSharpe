using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed class InvoiceCostRowViewModel
{
    public FormFieldsViewModel Fields { get; }
    public IRelayCommand RemoveCommand { get; }
    public InvoiceCostRowViewModel(MainWindowViewModel workspace, FormField[] fields)
    {
        Fields = new(fields.Select(field => new FormFieldViewModel(field)).ToArray(), 2);
        RemoveCommand = new RelayCommand(() => workspace.AdditionalCosts.Remove(fields));
    }
}

public sealed class InvoiceOptionsViewModel
{
    private readonly MainWindowViewModel workspace;
    public ObservableCollection<InvoiceCostRowViewModel> Costs { get; } = [];
    public FormFieldsViewModel DiscountFields { get; }
    public FormFieldsViewModel TaxFields { get; }
    public FormFieldsViewModel CustomFields { get; }
    public bool HasCustomFields => CustomFields.Fields.Count > 0;
    public FormFieldViewModel NotesField { get; }
    public FormFieldViewModel InterStateField { get; }
    public IRelayCommand AddCostCommand { get; }
    public InvoiceOptionsViewModel(MainWindowViewModel workspace)
    {
        this.workspace = workspace;
        DiscountFields = Fields(workspace.InvoiceOptions.Take(2), 2);
        TaxFields = Fields(workspace.InvoiceOptions.Skip(3));
        CustomFields = Fields(workspace.InvoiceCustomFields.Select(entry => entry.Field));
        NotesField = new(workspace.InvoiceOptions[2]);
        InterStateField = new(workspace.InterState);
        AddCostCommand = new RelayCommand(() => workspace.AdditionalCosts.Add(CreateCost()));
        RefreshCosts();
    }
    public static FormField[] CreateCost(string description = "", decimal amount = 0) =>
        [new("Description", description), new("Amount", amount.ToString(CultureInfo.CurrentCulture), "number", required: true) { AllowNegative = true, Help = "Use a negative amount for a deduction or adjustment." }];
    private static FormFieldsViewModel Fields(IEnumerable<FormField> fields, int columns = 1) => new(fields.Select(field => new FormFieldViewModel(field)).ToArray(), columns);
    public void Attach()
    {
        workspace.AdditionalCosts.CollectionChanged -= CostsChanged;
        workspace.AdditionalCosts.CollectionChanged += CostsChanged;
        RefreshCosts();
    }
    public void Detach() => workspace.AdditionalCosts.CollectionChanged -= CostsChanged;
    private void CostsChanged(object? sender, NotifyCollectionChangedEventArgs args) => RefreshCosts();
    private void RefreshCosts()
    {
        Costs.Clear();
        foreach (var fields in workspace.AdditionalCosts) Costs.Add(new(workspace, fields));
    }
}
