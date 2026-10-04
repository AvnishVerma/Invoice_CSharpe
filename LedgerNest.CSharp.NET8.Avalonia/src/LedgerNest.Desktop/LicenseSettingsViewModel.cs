using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public sealed partial class LicenseSettingsViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    [ObservableProperty] private string licenseDocument = "";
    [ObservableProperty] private bool isImporting;
    public string DeviceId => model.LicenseDeviceId;
    public bool CanCopy => DeviceId.Length > 0;
    public string StatusTitle => model.LicenseStatus.State switch
    {
        LicenseState.Missing => "Not activated", LicenseState.NotConfigured => "Activation unavailable",
        LicenseState.NotYetValid => "Not yet active", LicenseState.WrongDevice => "Different device",
        LicenseState.ClockError => "Check device time", LicenseState.StorageError => "License unavailable",
        _ => model.LicenseStatus.State.ToString()
    };
    public string StatusMessage => model.LicenseStatus.Message;
    public bool HasClaims => model.LicenseStatus.Claims != null;
    public string Customer => model.LicenseStatus.Claims?.Customer ?? "";
    public string LicenseId => model.LicenseStatus.Claims?.LicenseId ?? "";
    public string Expiry => model.LicenseStatus.Claims?.ExpiresAtUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm zzz") ?? "Perpetual";
    public string ChangeStatus => model.CanMakeBusinessChanges ? "Business changes are enabled." : "Existing records, PDF exports and backups remain available. A valid license is required to create or change business records.";

    public LicenseSettingsViewModel(MainWindowViewModel model)
    {
        this.model = model;
        model.RefreshLicense();
    }
    public void Attach()
    {
        model.PropertyChanged -= ModelChanged;
        model.PropertyChanged += ModelChanged;
        RefreshProperties();
    }
    public void Detach() => model.PropertyChanged -= ModelChanged;
    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(model.LicenseStatus)) RefreshProperties();
    }

    [RelayCommand] private void Refresh() { model.RefreshLicense(); RefreshProperties(); }
    [RelayCommand] private void Activate()
    {
        if (model.ActivateLicense(LicenseDocument)) LicenseDocument = "";
        RefreshProperties();
    }
    public void ActivateImported(string document) { model.ActivateLicense(document); RefreshProperties(); }
    public void SetStatus(string status) => model.Status = status;
    private void RefreshProperties()
    {
        OnPropertyChanged(nameof(DeviceId)); OnPropertyChanged(nameof(CanCopy)); OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusMessage)); OnPropertyChanged(nameof(HasClaims)); OnPropertyChanged(nameof(Customer));
        OnPropertyChanged(nameof(LicenseId)); OnPropertyChanged(nameof(Expiry)); OnPropertyChanged(nameof(ChangeStatus));
    }
}
