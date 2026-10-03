using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public sealed record ProductStockRow(int Id, string Code, string Name, string Unit, decimal Stock, bool Unlimited)
{
    public string Display => $"{Code} · {Name}";
    public string StockDisplay => Unlimited ? "Unlimited" : $"{Stock:0.###} {Unit}";
}

public sealed record InventoryMovementRow(DateTime Date, string Type, decimal Change, string Reference, string Notes, string User)
{
    public string ChangeDisplay => Change > 0 ? $"+{Change:0.###}" : $"{Change:0.###}";
}

public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? factory;
    private readonly Func<string> currentUser;
    private readonly Func<string> currentRole;
    private readonly Func<bool> canAdjust;
    public ObservableCollection<ProductStockRow> Products { get; } = [];
    public ObservableCollection<InventoryMovementRow> Movements { get; } = [];
    public string[] Directions { get; } = ["Stock In", "Stock Out"];
    public string[] MovementTypes { get; } = ["Adjustment", "Opening Stock", "Purchase", "Damage", "Transfer"];
    [ObservableProperty] private ProductStockRow? selectedProduct;
    [ObservableProperty] private string direction = "Stock In";
    [ObservableProperty] private string movementType = "Adjustment";
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private string reference = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string status = "";
    public bool CanManage => currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase) || canAdjust();

    public void RefreshAuthorizationState() => OnPropertyChanged(nameof(CanManage));

    public InventoryViewModel(IDbContextFactory<LedgerNestDbContext>? factory, Func<string> currentUser, Func<string> currentRole, Func<bool>? canAdjust = null)
    {
        this.factory = factory;
        this.currentUser = currentUser;
        this.currentRole = currentRole;
        this.canAdjust = canAdjust ?? (() => false);
        Refresh();
    }

    partial void OnSelectedProductChanged(ProductStockRow? value) => LoadMovements();

    [RelayCommand]
    private void Refresh()
    {
        var selectedId = SelectedProduct?.Id;
        Products.Clear();
        if (factory == null) return;
        using var db = factory.CreateDbContext();
        db.EnsureCurrentSchema();
        foreach (var product in db.Products.AsNoTracking().Where(product => product.Type != "Service").OrderBy(product => product.Name))
            Products.Add(new ProductStockRow(product.Id, product.ProductCode ?? "", product.Name,
                string.IsNullOrWhiteSpace(product.Unit) ? "None" : product.Unit, product.StockQuantity, product.UnlimitedStock));
        SelectedProduct = Products.FirstOrDefault(product => product.Id == selectedId) ?? Products.FirstOrDefault();
        LoadMovements();
    }

    private void LoadMovements()
    {
        Movements.Clear();
        if (factory == null || SelectedProduct == null) return;
        using var db = factory.CreateDbContext();
        foreach (var movement in db.InventoryTransactions.AsNoTracking().Where(item => item.ProductId == SelectedProduct.Id)
                     .OrderByDescending(item => item.TransactionDate).ThenByDescending(item => item.Id).Take(250))
            Movements.Add(new InventoryMovementRow(movement.TransactionDate, movement.TransactionType,
                movement.BaseQuantityChange, movement.Reference, movement.Notes, movement.CreatedBy));
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        if (!CanManage) { Error = "Only administrators can adjust inventory."; return; }
        if (factory == null || SelectedProduct == null) { Error = "Select a product."; return; }
        if (SelectedProduct.Unlimited) { Error = "Unlimited-stock products do not require inventory adjustments."; return; }
        if (Quantity <= 0) { Error = "Quantity must be greater than zero."; return; }
        IsBusy = true; Error = "";
        try
        {
            var change = Direction == "Stock Out" ? -Quantity : Quantity;
            await new InventoryService(factory).PostAsync(new InventoryTransaction
            {
                ProductId = SelectedProduct.Id,
                TransactionType = MovementType,
                BaseQuantityChange = change,
                Reference = Reference.Trim(),
                Notes = Notes.Trim(),
                CreatedBy = currentUser()
            });
            Status = $"Inventory adjusted by {change:+0.###;-0.###;0}.";
            Quantity = 1; Reference = ""; Notes = "";
            Refresh();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
