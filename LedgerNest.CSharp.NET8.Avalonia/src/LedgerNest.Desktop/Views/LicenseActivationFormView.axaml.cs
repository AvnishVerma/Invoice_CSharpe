using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Renders the reusable license activation form while callers supply its live controls.</summary>
public sealed partial class LicenseActivationFormView : UserControl
{
    public LicenseActivationFormView()
    {
        InitializeComponent();
    }

    public LicenseActivationFormView(Control status, Control deviceId, Control copy, Control import, Control paste, Control activate, Control refresh)
        : this()
    {
        StatusHost.Content = status;
        DeviceIdHost.Content = deviceId;
        CopyHost.Content = copy;
        ImportHost.Content = import;
        PasteHost.Content = paste;
        ActivateHost.Content = activate;
        RefreshHost.Content = refresh;
    }
}
