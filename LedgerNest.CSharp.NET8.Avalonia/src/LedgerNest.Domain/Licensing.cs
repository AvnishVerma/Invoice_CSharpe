namespace LedgerNest.Domain;

public static class LicenseFeatures
{
    public const string BusinessWrite = "business.write";
    public const string ProductId = "LedgerNest.Desktop";
    public const int MaximumFileBytes = 64 * 1024;
}

public sealed record LicenseClaims
{
    public int Version { get; init; } = 1;
    public string Product { get; init; } = LicenseFeatures.ProductId;
    public string LicenseId { get; init; } = "";
    public string Customer { get; init; } = "";
    public string DeviceId { get; init; } = "";
    public string Kind { get; init; } = "Paid";
    public DateTimeOffset IssuedAtUtc { get; init; }
    public DateTimeOffset NotBeforeUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public string[] Features { get; init; } = [];
}

// Signature covers the decoded payload bytes exactly, not reserialized JSON.
public sealed record SignedLicense(string Payload, string Signature);
public enum LicenseState { NotConfigured, Missing, Active, Trial, Expired, NotYetValid, Invalid, WrongDevice, ClockError, StorageError }

public sealed record LicenseStatus(LicenseState State, string Message, LicenseClaims? Claims = null)
{
    public bool Allows(string feature) => State is LicenseState.Active or LicenseState.Trial && Claims?.Features.Contains(feature, StringComparer.Ordinal) == true;
}

public sealed record StoredLicense(string Document, DateTimeOffset LastSeenUtc);

public interface ILicenseStore
{
    StoredLicense? Read();
    void Write(StoredLicense license);
}

public interface ILicenseService
{
    string DeviceId { get; }
    LicenseStatus GetStatus();
    LicenseStatus Activate(string document);
}
