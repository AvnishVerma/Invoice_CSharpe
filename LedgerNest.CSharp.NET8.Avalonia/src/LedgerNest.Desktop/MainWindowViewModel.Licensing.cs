using CommunityToolkit.Mvvm.ComponentModel;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    private readonly ILicenseService licenseService;
    [ObservableProperty] private LicenseStatus licenseStatus = new(LicenseState.Missing, "Import a license file to activate LedgerNest.");
    public string LicenseDeviceId => licenseService.DeviceId;
    public bool CanMakeBusinessChanges => LicenseStatus.Allows(LicenseFeatures.BusinessWrite);
    public bool ShowLicenseNotice => !CanMakeBusinessChanges || LicenseStatus.State == LicenseState.Trial;
    public string LicenseNotice => CanMakeBusinessChanges
        ? "Trial active · Expires " + LicenseStatus.Claims?.ExpiresAtUtc?.ToLocalTime().ToString("dd MMM yyyy")
        : "Read-only · Activate a license to create or change business records.";

    partial void OnLicenseStatusChanged(LicenseStatus value)
    {
        OnPropertyChanged(nameof(CanMakeBusinessChanges));
        OnPropertyChanged(nameof(ShowLicenseNotice));
        OnPropertyChanged(nameof(LicenseNotice));
    }

    public void RefreshLicense() => LicenseStatus = licenseService.GetStatus();

    public bool ActivateLicense(string document)
    {
        var result = licenseService.Activate(document);
        RefreshLicense();
        Status = result.State is LicenseState.Active or LicenseState.Trial ? "License activated successfully." : result.Message;
        return result.State is LicenseState.Active or LicenseState.Trial;
    }

    private bool RequireBusinessLicense()
    {
        RefreshLicense();
        if (CanMakeBusinessChanges) return true;
        var reason = LicenseStatus.State is LicenseState.Active or LicenseState.Trial ? "This license does not include business changes." : LicenseStatus.Message;
        Status = reason + " Open Settings → License. Existing records and backups remain available.";
        return false;
    }
}
