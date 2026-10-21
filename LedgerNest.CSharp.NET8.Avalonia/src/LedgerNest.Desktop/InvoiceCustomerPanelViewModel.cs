using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class InvoiceCustomerPanelViewModel : ObservableObject
{
    public MainWindowViewModel Workspace { get; }
    public FormFieldViewModel NameField { get; }
    public FormFieldViewModel PhoneField { get; }
    public FormFieldsViewModel DetailFields { get; }
    private readonly FormFieldViewModel[] customerFields;
    public FormFieldsViewModel CustomerFields => new(customerFields, CustomerColumns);
    [ObservableProperty] private int customerColumns = 3;
    partial void OnCustomerColumnsChanged(int value) => OnPropertyChanged(nameof(CustomerFields));
    public IRelayCommand SelectCommand { get; }
    public IRelayCommand CreateCommand { get; }
    public IRelayCommand EditDetailsCommand { get; }
    public IRelayCommand FinishEditingCommand { get; }
    public bool CanCreateCustomer => Workspace.CanPerformAction("Customer", "Add");
    [ObservableProperty] private bool isEditingCustomer;
    public IRelayCommand ToggleLockCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand SaveCustomerCommand { get; }
    [ObservableProperty] private bool detailsExpanded = true;
    public string DetailsIcon => DetailsExpanded ? "expand_less" : "expand_more";
    partial void OnDetailsExpandedChanged(bool value) => OnPropertyChanged(nameof(DetailsIcon));
    public InvoiceCustomerPanelViewModel(MainWindowViewModel workspace, Action select, Action<FormField>? expand = null, Action? create = null)
    {
        Workspace = workspace; NameField = new(workspace.InvoiceCustomer[0]); PhoneField = new(workspace.InvoiceCustomer[2]);
        customerFields = new[] { 0, 1, 2, 4, 3, 5 }.Where(index => index != 4 || workspace.InvoiceSetting("Show GST fields").IsChecked)
            .Select(index => new FormFieldViewModel(workspace.InvoiceCustomer[index], index switch { 0 => "Customer Name", 4 => "GSTIN / VAT", _ => workspace.InvoiceCustomer[index].Label }, singleLine: true, expand: index == 5 ? expand : null)).ToArray();
        DetailFields = new(workspace.InvoiceCustomer.Where((_, index) => index is not (0 or 2)
            && (index != 4 || workspace.InvoiceSetting("Show GST fields").IsChecked)).Select(field => new FormFieldViewModel(field)).ToArray(), 2);
        SelectCommand = new RelayCommand(select);
        CreateCommand = new RelayCommand(() => { if (CanCreateCustomer) create?.Invoke(); }, () => CanCreateCustomer && create != null);
        EditDetailsCommand = new RelayCommand(() => IsEditingCustomer = true);
        FinishEditingCommand = new RelayCommand(() => IsEditingCustomer = false);
        ToggleLockCommand = new RelayCommand(() => workspace.CustomerFieldsUnlocked = !workspace.CustomerFieldsUnlocked);
        ClearCommand = new RelayCommand(workspace.ClearInvoiceCustomer);
        RefreshCommand = new RelayCommand(() => workspace.RefreshInvoiceCustomer());
        SaveCustomerCommand = new RelayCommand(() => workspace.SaveInvoiceCustomer());
    }
    public void Attach() { Workspace.InvoiceCustomerStateChanged -= Reset; Workspace.InvoiceCustomerStateChanged += Reset; }
    public void Detach() => Workspace.InvoiceCustomerStateChanged -= Reset;
    private void Reset() { DetailsExpanded = true; IsEditingCustomer = false; }
}
