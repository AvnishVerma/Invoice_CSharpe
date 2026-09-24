namespace LedgerNest.LicenseManager;

public interface IPublisherFiles
{
    Task<string?> OpenPrivateKeyAsync();
    Task<string?> SaveLicenseAsync();
    Task<string?> SelectKeyFolderAsync();
    Task<string?> SavePublicKeyAsync();
}
