using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public sealed partial class UnitMasterViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? factory;
    private readonly Func<string> currentUser;
    private readonly Func<string> currentRole;
    private readonly Func<string, bool> hasPermission;
    public ObservableCollection<UnitOfMeasure> Units { get; } = [];
    [ObservableProperty] private UnitOfMeasure? selectedUnit;
    [ObservableProperty] private int editingId;
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string unitName = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string status = "";
    private bool IsAdmin => currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool CanAdd => IsAdmin || hasPermission("Add");
    public bool CanUpdate => IsAdmin || hasPermission("Update");
    public bool CanDelete => IsAdmin || hasPermission("Delete");
    public bool CanManage => CanAdd || CanUpdate || CanDelete;

    public void RefreshAuthorizationState()
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanManage));
    }

    public UnitMasterViewModel(IDbContextFactory<LedgerNestDbContext>? factory, Func<string> currentUser, Func<string> currentRole, Func<string, bool>? hasPermission = null)
    {
        this.factory = factory;
        this.currentUser = currentUser;
        this.currentRole = currentRole;
        this.hasPermission = hasPermission ?? (_ => false);
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Units.Clear();
        if (factory == null) return;
        using var db = factory.CreateDbContext();
        db.EnsureCurrentSchema();
        foreach (var unit in db.Units.AsNoTracking().OrderBy(item => item.Code)) Units.Add(unit);
    }

    [RelayCommand]
    private void New()
    {
        if (!currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase) && !hasPermission("Add")) { Error = "You cannot create units."; return; }
        EditingId = 0; Code = ""; UnitName = ""; Description = ""; IsActive = true; Error = ""; IsEditing = true;
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (!currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase) && !hasPermission("Update")) { Error = "You cannot edit units."; return; }
        if (SelectedUnit == null) return;
        EditingId = SelectedUnit.Id; Code = SelectedUnit.Code; UnitName = SelectedUnit.Name;
        Description = SelectedUnit.Description; IsActive = SelectedUnit.IsActive; Error = ""; IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() { IsEditing = false; Error = ""; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (EditingId == 0 ? !CanAdd : !CanUpdate) { Error = EditingId == 0 ? "You cannot create units." : "You cannot edit units."; return; }
        if (factory == null) { Error = "Database is unavailable."; return; }
        IsBusy = true; Error = "";
        try
        {
            var unit = new UnitOfMeasure { Id = EditingId, Code = Code, Name = UnitName, Description = Description, IsActive = IsActive };
            await new UnitMasterService(factory).SaveAsync(unit, currentUser(), currentRole());
            Status = EditingId == 0 ? "Unit created." : "Unit updated.";
            IsEditing = false;
            Refresh();
            SelectedUnit = Units.FirstOrDefault(item => item.Id == unit.Id);
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (!CanDelete) { Error = "You cannot delete units."; return; }
        if (factory == null || SelectedUnit == null) return;
        IsBusy = true; Error = "";
        try
        {
            await new UnitMasterService(factory).DeleteAsync(SelectedUnit.Id, currentRole(), currentUser());
            Status = "Unit deleted.";
            Refresh();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
