using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class InvoiceCustomerPanelViewModel : ObservableObject
{
    public MainWindowViewModel Workspace { get; }
    public FormFieldViewModel NameField { get; }
    public FormFieldViewModel PhoneField { get; }
    public FormFieldsViewModel DetailFields { get; }
    public IRelayCommand SelectCommand { get; }
    public IRelayCommand ToggleLockCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand SaveCustomerCommand { get; }
    [ObservableProperty] private bool detailsExpanded;
    public string DetailsIcon => DetailsExpanded ? "expand_less" : "expand_more";
    partial void OnDetailsExpandedChanged(bool value) => OnPropertyChanged(nameof(DetailsIcon));
    public InvoiceCustomerPanelViewModel(MainWindowViewModel workspace, Action select)
    {
        Workspace = workspace; NameField = new(workspace.InvoiceCustomer[0]); PhoneField = new(workspace.InvoiceCustomer[2]);
        DetailFields = new(workspace.InvoiceCustomer.Where((_, index) => index is not (0 or 2)
            && (index != 4 || workspace.InvoiceSetting("Show GST fields").IsChecked)).Select(field => new FormFieldViewModel(field)).ToArray(), 2);
        SelectCommand = new RelayCommand(select);
        ToggleLockCommand = new RelayCommand(() => workspace.CustomerFieldsUnlocked = !workspace.CustomerFieldsUnlocked);
        ClearCommand = new RelayCommand(workspace.ClearInvoiceCustomer);
        RefreshCommand = new RelayCommand(() => workspace.RefreshInvoiceCustomer());
        SaveCustomerCommand = new RelayCommand(() => workspace.SaveInvoiceCustomer());
    }
    public void Attach() { Workspace.InvoiceCustomerStateChanged -= Reset; Workspace.InvoiceCustomerStateChanged += Reset; }
    public void Detach() => Workspace.InvoiceCustomerStateChanged -= Reset;
    private void Reset() => DetailsExpanded = false;
}
