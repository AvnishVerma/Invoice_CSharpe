using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

// Keeps session invalidation and active-company commit independently testable from windows.
public static class CompanySessionTransition
{
    public static void Switch(CompanyRegistryService registry, MainWindowViewModel? previous, MainWindowViewModel next, Action present)
    {
        var state = registry.Read();
        var target = next.CompanyContext?.ActiveCompany ?? throw new InvalidOperationException("The target workspace has no company identity.");
        if (next.CurrentUsername != null) throw new InvalidOperationException("Company switching requires a fresh signed-out workspace.");
        if (previous?.CompanyContext != null && previous.CompanyContext.ActiveCompany.Id != state.ActiveCompanyId)
            throw new InvalidOperationException("This workspace is stale. Reopen the active company before switching.");
        var targetProfile = state.Companies.SingleOrDefault(company => company.Id == target.Id)
            ?? throw new InvalidOperationException("The target company no longer exists.");
        if (targetProfile.Database != target.Database) throw new InvalidOperationException("The target database profile changed. Refresh before switching.");
        previous?.SignOut();
        registry.Activate(target.Id);
        try { present(); }
        catch
        {
            registry.Activate(state.ActiveCompanyId);
            throw;
        }
    }
}
