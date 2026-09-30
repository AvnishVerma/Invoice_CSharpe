using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public sealed record ProductOption(int Id, string Name) { public override string ToString() => Name; }
public sealed record UnitOption(int Id, string Code, string Name) { public override string ToString() => $"{Code} · {Name}"; }
public sealed record SellingUnitRow(int Id, string Unit, decimal Conversion, decimal Price, bool IsDefault, bool IsActive);
public sealed record PriceRow(long Id, string Product, string Unit, string PriceList, decimal Price, DateTime EffectiveDate, bool IsActive);

public sealed partial class ProductPriceMasterViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? factory;
    private readonly Func<string> user;
    private readonly Func<string> role;
    public ObservableCollection<ProductOption> Products { get; } = [];
    public ObservableCollection<UnitOption> Units { get; } = [];
    public ObservableCollection<SellingUnitRow> SellingUnits { get; } = [];
    public ObservableCollection<PriceRow> Prices { get; } = [];
    [ObservableProperty] private ProductOption? selectedProduct;
    [ObservableProperty] private UnitOption? selectedUnit;
    [ObservableProperty] private SellingUnitRow? selectedSellingUnit;
    [ObservableProperty] private decimal conversionFactor = 1;
    [ObservableProperty] private decimal unitSellingPrice;
    [ObservableProperty] private decimal effectiveSellingPrice;
    [ObservableProperty] private bool isDefault;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private string priceList = "Default";
    [ObservableProperty] private DateTimeOffset? effectiveDate = DateTimeOffset.Now;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string status = "";
    public bool CanManage => role().Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool HasProduct => SelectedProduct != null;

    public ProductPriceMasterViewModel(IDbContextFactory<LedgerNestDbContext>? factory, Func<string> user, Func<string> role)
    {
        this.factory = factory; this.user = user; this.role = role;
        Refresh();
    }

    partial void OnSelectedProductChanged(ProductOption? value) { OnPropertyChanged(nameof(HasProduct)); RefreshDetails(); }

    [RelayCommand]
    private void Refresh()
    {
        Products.Clear(); Units.Clear();
        if (factory == null) return;
        using var db = factory.CreateDbContext(); db.EnsureCurrentSchema();
        foreach (var product in db.Products.AsNoTracking().OrderBy(item => item.Name)) Products.Add(new(product.Id, product.Name));
        foreach (var unit in db.Units.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)) Units.Add(new(unit.Id, unit.Code, unit.Name));
        if (SelectedProduct != null) SelectedProduct = Products.FirstOrDefault(item => item.Id == SelectedProduct.Id);
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        SellingUnits.Clear(); Prices.Clear();
        if (factory == null || SelectedProduct == null) return;
        using var db = factory.CreateDbContext();
        var units = db.Units.AsNoTracking().ToDictionary(item => item.Id);
        foreach (var item in db.ProductSellingUnits.AsNoTracking().Where(item => item.ProductId == SelectedProduct.Id).OrderByDescending(item => item.IsDefault).ThenBy(item => item.Id))
            SellingUnits.Add(new(item.Id, units.GetValueOrDefault(item.UnitId)?.Code ?? "", item.ConversionFactor, item.SellingPrice, item.IsDefault, item.IsActive));
        var sellingUnitNames = db.ProductSellingUnits.AsNoTracking().Where(item => item.ProductId == SelectedProduct.Id).ToDictionary(item => item.Id, item => units.GetValueOrDefault(item.UnitId)?.Code ?? "");
        foreach (var item in db.ProductPrices.AsNoTracking().Where(item => item.ProductId == SelectedProduct.Id).OrderByDescending(item => item.EffectiveDate))
            Prices.Add(new(item.Id, SelectedProduct.Name, sellingUnitNames.GetValueOrDefault(item.SellingUnitId, ""), item.PriceList, item.SellingPrice, item.EffectiveDate, item.IsActive));
    }

    [RelayCommand]
    private async Task SaveSellingUnitAsync()
    {
        if (factory == null || SelectedProduct == null || SelectedUnit == null) { Error = "Select a product and unit."; return; }
        IsBusy = true; Error = "";
        try
        {
            await new ProductPricingService(factory).SaveSellingUnitAsync(new ProductSellingUnit { ProductId = SelectedProduct.Id, UnitId = SelectedUnit.Id, ConversionFactor = ConversionFactor, SellingPrice = UnitSellingPrice, IsDefault = IsDefault, IsActive = IsActive }, role());
            Status = "Selling unit saved."; RefreshDetails();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SavePriceAsync()
    {
        if (factory == null || SelectedProduct == null || SelectedSellingUnit == null) { Error = "Select a product and selling unit."; return; }
        IsBusy = true; Error = "";
        try
        {
            await new ProductPricingService(factory).SavePriceAsync(new ProductPrice { ProductId = SelectedProduct.Id, SellingUnitId = SelectedSellingUnit.Id, PriceList = PriceList, SellingPrice = EffectiveSellingPrice, EffectiveDate = EffectiveDate?.UtcDateTime ?? DateTime.UtcNow, IsActive = IsActive }, user(), role());
            Status = "Effective price saved."; RefreshDetails();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
