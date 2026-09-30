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
    public bool CanManage => currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase);

    public UnitMasterViewModel(IDbContextFactory<LedgerNestDbContext>? factory, Func<string> currentUser, Func<string> currentRole)
    {
        this.factory = factory;
        this.currentUser = currentUser;
        this.currentRole = currentRole;
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
        EditingId = 0; Code = ""; UnitName = ""; Description = ""; IsActive = true; Error = ""; IsEditing = true;
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedUnit == null) return;
        EditingId = SelectedUnit.Id; Code = SelectedUnit.Code; UnitName = SelectedUnit.Name;
        Description = SelectedUnit.Description; IsActive = SelectedUnit.IsActive; Error = ""; IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() { IsEditing = false; Error = ""; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManage) { Error = "Only administrators can manage units."; return; }
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
        if (!CanManage) { Error = "Only administrators can manage units."; return; }
        if (factory == null || SelectedUnit == null) return;
        IsBusy = true; Error = "";
        try
        {
            await new UnitMasterService(factory).DeleteAsync(SelectedUnit.Id, currentRole());
            Status = "Unit deleted.";
            Refresh();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
