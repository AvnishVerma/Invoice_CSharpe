using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop;

public sealed class InvoiceCostRowViewModel
{
    public FormFieldsViewModel Fields { get; }
    public IRelayCommand RemoveCommand { get; }
    public FormFieldViewModel LabelField => Fields.Fields[0];
    public FormFieldViewModel AmountField => Fields.Fields[1];
    public InvoiceCostRowViewModel(MainWindowViewModel workspace, FormField[] fields)
    {
        Fields = new([new FormFieldViewModel(fields[0], "Label"), new FormFieldViewModel(fields[1], "Amount")], 2);
        RemoveCommand = new RelayCommand(() => workspace.AdditionalCosts.Remove(fields));
    }
}

public sealed class InvoiceOptionsViewModel : ObservableObject
{
    private readonly MainWindowViewModel workspace;
    public ObservableCollection<InvoiceCostRowViewModel> Costs { get; } = [];
    public FormFieldsViewModel DiscountFields { get; }
    public FormFieldViewModel DiscountAmount => DiscountFields.Fields[1];
    public string[] DiscountModes { get; } = ["None", "%", "Amount"];
    public string DiscountMode
    {
        get => workspace.InvoiceOptions[0].Value == "Percentage" ? "%" : workspace.InvoiceOptions[0].Value;
        set => workspace.InvoiceOptions[0].Value = value == "%" ? "Percentage" : value;
    }
    public int CostCount => Costs.Count;
    public bool HasCosts => CostCount > 0;
    public FormFieldsViewModel TaxFields { get; }
    public FormFieldsViewModel CustomFields { get; }
    public bool HasCustomFields => CustomFields.Fields.Count > 0;
    public FormFieldViewModel NotesField { get; }
    public FormFieldViewModel InterStateField { get; }
    public IRelayCommand AddCostCommand { get; }
    public InvoiceOptionsViewModel(MainWindowViewModel workspace, Action<FormField>? expand = null)
    {
        this.workspace = workspace;
        DiscountFields = new([new(workspace.InvoiceOptions[0]), new(workspace.InvoiceOptions[1], "Invoice Discount")], 2);
        TaxFields = Fields(workspace.InvoiceOptions.Skip(3));
        CustomFields = Fields(workspace.InvoiceCustomFields.Select(entry => entry.Field));
        NotesField = new(workspace.InvoiceOptions[2], "Notes (optional)", expand: expand);
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
        OnPropertyChanged(nameof(CostCount)); OnPropertyChanged(nameof(HasCosts));
    }
}
