using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.LicensePublisher;
using System.Security.Cryptography;

namespace LedgerNest.LicenseManager;

public partial class PublisherViewModel(IPublisherFiles files) : ObservableObject
{
    [ObservableProperty] private string privateKeyPath = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string customer = "";
    [ObservableProperty] private string deviceId = "";
    [ObservableProperty] private int licenseKindIndex;
    [ObservableProperty] private decimal? durationDays = 365;
    [ObservableProperty] private string newKeyPassword = "";
    [ObservableProperty] private string confirmPassword = "";
    [ObservableProperty] private string status = "Ready. Select your signing key and enter the customer's details.";
    [ObservableProperty] private string result = "";
    [ObservableProperty] private bool isBusy;

    public bool IsIdle => !IsBusy;
    public bool HasDuration => LicenseKindIndex != 2;
    public decimal MaximumDays => LicenseKindIndex == 1 ? 30 : 36500;
    public string DurationHint => LicenseKindIndex == 1 ? "Trial access for 1–30 days, starting when issued." : "Access starts when issued. All expiration times use UTC.";
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsIdle));
    partial void OnLicenseKindIndexChanged(int value)
    {
        DurationDays = value == 1 ? 30 : 365;
        OnPropertyChanged(nameof(HasDuration));
        OnPropertyChanged(nameof(MaximumDays));
        OnPropertyChanged(nameof(DurationHint));
    }

    [RelayCommand]
    private async Task BrowseKeyAsync() => await RunAsync(async () =>
    {
        var path = await files.OpenPrivateKeyAsync();
        if (path != null) PrivateKeyPath = path;
    });

    [RelayCommand]
    private async Task IssueAsync() => await RunAsync(async () =>
    {
        var trial = LicenseKindIndex == 1;
        var perpetual = LicenseKindIndex == 2;
        if (LicenseKindIndex is < 0 or > 2) throw new ArgumentException("Choose a license type.");
        if (!perpetual && (DurationDays == null || DurationDays != decimal.Truncate(DurationDays.Value)))
            throw new ArgumentException("Enter a whole number of days.");
        var days = perpetual ? 365 : checked((int)DurationDays!.Value);
        LicenseIssuer.ValidateRequest(Customer, DeviceId, trial, perpetual, days);
        if (string.IsNullOrWhiteSpace(PrivateKeyPath) || string.IsNullOrEmpty(Password))
            throw new ArgumentException("Select an encrypted private key and enter its password.");
        var path = await files.SaveLicenseAsync();
        if (path == null) { Status = "Issuance cancelled. No license was saved."; return; }
        var keyPath = PrivateKeyPath; var secret = Password; var name = Customer; var device = DeviceId;
        var issued = await Task.Run(() =>
        {
            var license = LicenseIssuer.Issue(ReadKey(keyPath), secret, name, device, trial, perpetual, days);
            LicenseIssuer.WriteNew(path, license.Document);
            return license;
        });
        Status = "License issued successfully. Send the .ledgerlicense file to the customer.";
        Result = $"Customer: {issued.Claims.Customer}\nLicense ID: {issued.Claims.LicenseId}\nType: {(perpetual ? "Perpetual paid" : issued.Claims.Kind)}\nExpires: {issued.Claims.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "Never"}\nSaved to: {path}";
    }, clearSigningPassword: true);

    [RelayCommand]
    private async Task GenerateKeysAsync() => await RunAsync(async () =>
    {
        if (NewKeyPassword.Length < 16) throw new ArgumentException("Use a signing password of at least 16 characters.");
        if (NewKeyPassword != ConfirmPassword) throw new ArgumentException("The new-key passwords do not match.");
        var folder = await files.SelectKeyFolderAsync();
        if (folder == null) { Status = "Key generation cancelled. No keys were created."; return; }
        var privatePath = Path.Combine(folder, "issuer.private.pem");
        var publicPath = Path.Combine(folder, "public-key.pem");
        if (File.Exists(privatePath) || File.Exists(publicPath)) throw new ArgumentException("This folder already contains a key file. Select a different folder to preserve your existing keys.");
        var secret = NewKeyPassword;
        await Task.Run(() => LicenseIssuer.SaveKeys(privatePath, publicPath, LicenseIssuer.GenerateKeys(secret)));
        PrivateKeyPath = privatePath;
        Status = "Signing keys created. The private key is selected on the Issue license tab.";
        Result = $"Encrypted private key: {privatePath}\nPublic verification key: {publicPath}\n\nKeep the private key and its password with the publisher. Embed the public key in your customer app before publishing it.";
    }, clearNewPassword: true);

    [RelayCommand]
    private async Task ExportPublicKeyAsync() => await RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(PrivateKeyPath) || string.IsNullOrEmpty(Password))
            throw new ArgumentException("Select your private key and enter its password on the Issue license tab first.");
        var path = await files.SavePublicKeyAsync();
        if (path == null) { Status = "Public-key export cancelled."; return; }
        var keyPath = PrivateKeyPath; var secret = Password;
        await Task.Run(() => LicenseIssuer.WriteNew(path, LicenseIssuer.ExportPublicKey(ReadKey(keyPath), secret)));
        Status = "Public verification key exported.";
        Result = $"Public verification key: {path}";
    }, clearSigningPassword: true);

    public void ClearPasswords() { Password = ""; NewKeyPassword = ""; ConfirmPassword = ""; }

    private static string ReadKey(string path)
    {
        if (new FileInfo(path).Length > 65536) throw new ArgumentException("The selected file is too large to be a signing key.");
        return File.ReadAllText(path);
    }

    private async Task RunAsync(Func<Task> action, bool clearSigningPassword = false, bool clearNewPassword = false)
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Working…";
        Result = "";
        try { await action(); }
        catch (CryptographicException) { Status = "Unable to unlock the signing key. Check the encrypted key file and password."; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or OverflowException or NotSupportedException)
        { Status = "Could not complete the operation: " + ex.Message; }
        finally
        {
            if (clearSigningPassword) Password = "";
            if (clearNewPassword) { NewKeyPassword = ""; ConfirmPassword = ""; }
            IsBusy = false;
        }
    }
}
