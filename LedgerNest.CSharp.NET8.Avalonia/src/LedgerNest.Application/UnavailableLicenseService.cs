using LedgerNest.Domain;

namespace LedgerNest.Application;

public sealed class UnavailableLicenseService(string message = "License verification is not configured in this build. Contact your software provider.") : ILicenseService
{
    public string DeviceId => "";
    public LicenseStatus GetStatus() => new(LicenseState.NotConfigured, message);
    public LicenseStatus Activate(string document) => GetStatus();
}
