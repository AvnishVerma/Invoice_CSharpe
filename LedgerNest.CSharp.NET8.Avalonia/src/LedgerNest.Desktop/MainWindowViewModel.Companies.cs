using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public CompanyWorkspaceContext? CompanyContext { get; }
    public CompanyManagementViewModel? CompanyManagement { get; }
    public bool IsAdministrator => sessionAccount != null && !RequiresPasswordChange && dbFactory != null
        && new AuthorizationService(dbFactory).GetUserRolesAsync(sessionAccount.Id).GetAwaiter().GetResult().Contains("Admin", StringComparer.OrdinalIgnoreCase);
    internal bool RequireCompanyWriteLicense() => RequireBusinessLicense();
    private bool IsCurrentCompany()
    {
        if (CompanyContext == null) return true;
        try { return CompanyContext.Registry.Read().ActiveCompanyId == CompanyContext.ActiveCompany.Id; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { return false; }
    }

    private void EnsureCompanyIdentity()
    {
        if (CompanyContext == null || dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var saved = db.Settings.Find(CompanyRegistryService.IdentitySetting);
        if (saved != null && saved.Value != CompanyContext.ActiveCompany.Id)
            throw new InvalidOperationException("This database belongs to another workspace. Opening it was cancelled.");
        SetSetting(db, CompanyRegistryService.IdentitySetting, CompanyContext.ActiveCompany.Id);
        db.SaveChanges();
    }

    public string SuggestedBackupName(string extension) => CompanyContext == null
        ? $"ledgernest_backup_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}"
        : CompanyContext.Registry.BackupFileName(CompanyContext.ActiveCompany.Id, extension, DateTime.Now);
    public string? CompanyBackupDirectory => CompanyContext?.Registry.BackupDirectory(CompanyContext.ActiveCompany.Id);

    private void ValidateBackupCompany(string? id)
    {
        if (CompanyContext == null) return;
        if (string.IsNullOrWhiteSpace(id) && CompanyContext.ActiveCompany.IsOriginal) return;
        if (id != CompanyContext.ActiveCompany.Id)
            throw new InvalidOperationException("This backup does not belong to the current database and cannot be restored here.");
    }

    private bool CanUseCompanyBackup(bool restore)
    {
        if (CompanyContext == null) return true;
        if (!CanContinueWorkspaceOperation(SessionVersion) || !HasPermission("Settings", restore ? "Update" : "View"))
        { Status = "You do not have permission to use backups."; return false; }
        return !restore || RequireBusinessLicense();
    }
}
