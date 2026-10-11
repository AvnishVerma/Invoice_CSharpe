using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class SharedPageHeaderTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HeaderUsesSharedDimensionsAndSupportsCenteredTitle(bool centerTitle) => headless.Run(() =>
    {
        var header = new PageHeaderView { Title = "Dashboard Overview", CenterTitle = centerTitle };
        var window = new Window { Content = header, Width = 800, Height = 44.8 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var band = Assert.Single(header.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("page-header"));
            Assert.Equal(44.8, band.Height);
            Assert.InRange(Math.Abs(band.Bounds.Height - band.Height), 0, 0.5); // Device-pixel layout rounding.
            var title = header.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "Heading");
            Assert.Equal(20, title.FontSize);
            var point = title.TranslatePoint(default, header)!.Value;
            if (centerTitle) Assert.Equal(header.Bounds.Width / 2, point.X + title.Bounds.Width / 2, 1);
            else Assert.Equal(20, point.X, 1);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void MigratedScreensDeclareSharedHeaderRatherThanDuplicateBand()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "LedgerNest.CSharp.sln"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var screen in new[] { "DashboardPageView", "InvoiceEditorShellView", "InvoiceCreatedView", "InventoryView", "UnitMasterView", "ProductPriceMasterView", "ReportsPageView" })
        {
            var source = File.ReadAllText(Path.Combine(root!.FullName, "src", "LedgerNest.Desktop", "Views", screen + ".axaml"));
            Assert.Contains("PageHeaderView", source);
            Assert.DoesNotContain("Classes=\"page-header\"", source);
            Assert.DoesNotContain("page-header-title", source);
        }
    }
}
