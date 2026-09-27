using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the declarative company-information form structure while hosting live field controls.</summary>
public sealed partial class CompanyDetailsFormView : UserControl
{
    public CompanyDetailsFormView()
    {
        InitializeComponent();
    }

    public CompanyDetailsFormView(
        Control companyFields,
        Control businessType,
        Control qrSettings,
        Control upiRows,
        Control addUpi,
        Control bankSettings,
        Control bankRows,
        Control addBank)
        : this()
    {
        CompanyFieldsHost.Content = companyFields;
        BusinessTypeHost.Content = businessType;
        QrHost.Content = qrSettings;
        UpiRowsHost.Content = upiRows;
        AddUpiHost.Content = addUpi;
        BankSettingsHost.Content = bankSettings;
        BankRowsHost.Content = bankRows;
        AddBankHost.Content = addBank;
    }
}
