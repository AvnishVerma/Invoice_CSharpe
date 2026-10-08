namespace LedgerNest.Desktop;

public sealed class InvoiceCustomerSettingsViewModel
{
    public MasterCodeSettingsViewModel CodeSettings { get; }
    public FormField BusinessName { get; }
    public FormField Address { get; }
    public FormField Phone { get; }
    public FormField Email { get; }
    public FormField Gstin { get; }

    public InvoiceCustomerSettingsViewModel(MainWindowViewModel workspace)
    {
        CodeSettings = workspace.CreateMasterCodeSettings("Customer");
        BusinessName = workspace.InvoiceSetting("Show Customer Business Name");
        Address = workspace.InvoiceSetting("Show Customer Address");
        Phone = workspace.InvoiceSetting("Show Customer Phone");
        Email = workspace.InvoiceSetting("Show Customer Email");
        Gstin = workspace.InvoiceSetting("Show Customer GSTIN");
    }
}
