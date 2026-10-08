using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.UI;

public sealed class DashboardStockDataTests
{
    [Fact]
    public void StockListReadsCurrentDatabaseRatherThanCachedProducts()
    {
        using var fixture = new TestDatabaseFixture();
        using (var db = fixture.CreateDbContext())
        {
            db.Products.AddRange(new Product { Name = "Backpack", StockQuantity = 0 },
                new Product { Name = "Available", StockQuantity = 5 }, new Product { Name = "Unlimited", UnlimitedStock = true });
            db.SaveChanges();
        }
        var model = fixture.CreateModel(); Assert.Equal("Backpack", Assert.Single(model.GetDashboardStockProducts()).Name);
        using (var db = fixture.CreateDbContext())
        {
            db.Products.Single(product => product.Name == "Backpack").StockQuantity = 10;
            db.Products.Single(product => product.Name == "Available").StockQuantity = -2;
            db.SaveChanges();
        }
        var current = Assert.Single(model.GetDashboardStockProducts()); Assert.Equal("Available", current.Name); Assert.Equal("-2", current["Stock"]);
        Assert.Equal("0", model.Products.Single(product => product.Name == "Backpack")["Stock"]);
    }
}

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class DashboardStockListTests(HeadlessFixture headless)
{
    [Theory] [InlineData("classic")] [InlineData("bento")] [InlineData("simple")]
    public Task AlternativeLayouts_ShowEveryStockRowAndLiveCount(string layout) => headless.Run(() =>
    {
        var calls = new List<string>();
        var model = new DashboardPageModel([], [], [], [], [], "0", "0", "2", "Accounting Service", "admin", () => { }, layout,
            outOfStockProducts: [new("Accounting Service", "Service", "0", "Stock: 0", new RelayCommand(() => calls.Add("Service"))),
                new("Backpack", "Product", "1299", "Stock: 0", new RelayCommand(() => calls.Add("Backpack")))]);
        var view = new DashboardPageView(model); var window = new Window { Content = view, Width = 1144, Height = 1100 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var buttons = view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && ToolTip.GetTip(button)?.ToString() == "Restock").ToArray();
            Assert.Equal(2, buttons.Length); Assert.Equal("2", model.OutOfStockCount);
            foreach (var button in buttons) button.Command!.Execute(null);
            Assert.Equal(new[] { "Service", "Backpack" }, calls);
            var texts = view.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToArray();
            Assert.Contains("Accounting Service", texts); Assert.Contains("Backpack", texts); Assert.Contains("0 left", texts);
            model.OutOfStockProducts.RemoveAt(0); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal("1", model.OutOfStockCount);
            Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && ToolTip.GetTip(button)?.ToString() == "Restock");
            model.OutOfStockProducts.Clear(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal("0", model.OutOfStockCount); Assert.True(model.HasNoOutOfStock);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == "No products are out of stock");
        }
        finally { window.Close(); }
    });
}
