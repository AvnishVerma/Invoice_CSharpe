using LedgerNest.Application;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.Products;

public sealed class InventoryValuationParityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Valuation_DecimalUnitsCostRetailProfitAndExclusionsAreIndependentOfControls()
    {
        var result = InventoryValuationCalculator.Calculate([
            new("Tracked", 2.5m, 40m, 100m, "Product", false),
            new("Service", 100m, 20m, 40m, "Service", false),
            new("Unlimited", 100m, 20m, 40m, "Product", true)]);
        Assert.Equal(100, result.StockValue);
        Assert.Equal(250, result.SaleValue);
        Assert.Equal(150, result.PotentialProfit);
        Assert.Equal(2.5m, result.Units);
        Assert.Equal(2, result.ExcludedCount);
        Assert.Single(result.Items);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Valuation_PreservesExistingNonnegativeAvailableStockPolicy()
    {
        var result = InventoryValuationCalculator.Calculate([new("Backordered", -5, 20, 30, "Product", false)]);
        Assert.Equal(0, result.Units);
        Assert.Equal(0, result.StockValue);
        Assert.Equal(0, result.SaleValue);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Valuation_RefreshReadsDatabaseAndExportsAllRowsRegardlessOfPage()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext())
        {
            for (var index = 1; index <= 27; index++)
                db.Products.Add(new Product { Name = $"Stock-{index:D2}", StockQuantity = .5m, PurchasePrice = 2, SalePrice = 4 });
            db.Products.Add(new Product { Name = "Excluded-service", Type = "Service", StockQuantity = 100 });
            db.SaveChanges();
        }
        Assert.Empty(model.Products); // Simulate data added after the workspace was loaded.
        var report = model.BuildInventoryReport();
        Assert.Equal(27, report.ProductsTracked);
        Assert.Equal(13.5m, report.TotalUnits);
        var vm = new InventoryReportViewModel(report, () => Task.CompletedTask, () => Task.CompletedTask);
        Assert.Equal("13.5", vm.TotalUnits);
        Assert.Equal(25, vm.VisibleRows.Length);
        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.VisibleRows.Length);
        var csv = model.ExportReportCsv("Inventory");
        Assert.Contains("Stock-01", csv);
        Assert.Contains("Stock-27", csv);
        Assert.DoesNotContain("Excluded-service", csv);
        Assert.Contains("\"TOTAL\",\"13.5\"", csv);
        var bytes = model.ExportReportPdf("Inventory");
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(bytes, new Docnet.Core.Models.PageDimensions(1));
        var text = string.Join("\n", Enumerable.Range(0, reader.GetPageCount()).Select(index =>
        { using var page = reader.GetPageReader(index); return page.GetText(); }));
        Assert.Contains("Stock-01", text);
        Assert.Contains("Stock-27", text);
        Assert.Contains("Units: 13.5", text);
        Assert.Contains("excluded: 1", text);
    }
}
