using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Application;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.UI;

public sealed class DashboardLayoutRulesTests
{
    [Theory] [InlineData("default", "Default")] [InlineData("CLASSIC", "Classic")]
    [InlineData("bento", "Bento")] [InlineData("simple", "Simple")] [InlineData(" Simple Feed ", "Simple")]
    [Trait("Category", "Unit")]
    public void ExistingNames_MapToSupportedLayouts(string stored, string display) => Assert.Equal(display, DashboardLayoutRules.DisplayName(stored));

    [Theory] [InlineData(null)] [InlineData("")] [InlineData("unknown")]
    [Trait("Category", "Unit")]
    public void InvalidSelections_AreNotAccepted(string? value) => Assert.Null(DashboardLayoutRules.Parse(value));

    [Fact] [Trait("Category", "ViewModel")]
    public void FailedSelectionSave_DoesNotChangeDisplayedLayout()
    {
        var state = new DashboardPageModel([], [], [], [], [], "0", "0", "0", "", "admin", () => { }, "classic", saveLayout: _ => false);
        state.SelectLayoutCommand.Execute("bento"); Assert.Equal("Classic", state.Layout);
    }
}

public sealed class DashboardLayoutPersistenceTests
{
    [Theory] [InlineData("default")] [InlineData("classic")] [InlineData("bento")] [InlineData("simple")]
    [Trait("Category", "Integration")]
    public void SavedLayout_SurvivesReloadRefreshNavigationAndLogin(string layout)
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        using (var db = fixture.CreateDbContext()) { db.Settings.Add(new AppSetting { Key = "dashboard_layout", Value = layout }); db.SaveChanges(); }
        var reload = fixture.CreateModel(); Assert.Equal(layout, reload.DashboardLayout);
        reload.RefreshPersistedData(); reload.NavigateCommand.Execute("Customers"); reload.NavigateCommand.Execute("Dashboard");
        reload.SignOut(); Assert.True(reload.SignIn("admin", "admin"));
        Assert.Equal(layout, reload.DashboardLayout);
        using var check = fixture.CreateDbContext(); Assert.Equal(layout, check.Settings.Find("dashboard_layout")!.Value);
    }

    [Theory] [InlineData("Default", "default")] [InlineData("Classic", "classic")] [InlineData("Bento", "bento")] [InlineData("Simple", "simple")] [InlineData("Simple Feed", "simple")]
    [Trait("Category", "ViewModel")]
    public void SelectingLayout_PersistsCanonicalValueWithoutChangingInvoiceLayout(string display, string value)
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        var before = workspace.Settings.SelectMany(group => group.Value.SelectMany(section => section.Fields)).ToDictionary(field => field, field => field.Value);
        Assert.True(workspace.SetDashboardLayout(display), workspace.Status);
        Assert.Equal(value, fixture.CreateModel().DashboardLayout);
        using var db = fixture.CreateDbContext(); Assert.Equal(value, db.Settings.Find("dashboard_layout")!.Value);
        foreach (var field in before) Assert.Equal(field.Value, field.Key.Value);
    }

    [Theory] [InlineData("Classic", "classic")] [InlineData("Simple Feed", "simple")] [InlineData("future-layout", "default")]
    [Trait("Category", "Integration")]
    public void ExistingSavedValue_IsReadCompatiblyWithoutRewritingIt(string saved, string selected)
    {
        using var fixture = new TestDatabaseFixture(); fixture.CreateModel();
        using (var db = fixture.CreateDbContext()) { db.Settings.Add(new AppSetting { Key = "dashboard_layout", Value = saved }); db.SaveChanges(); }
        Assert.Equal(selected, fixture.CreateModel().DashboardLayout);
        using var check = fixture.CreateDbContext(); Assert.Equal(saved, check.Settings.Find("dashboard_layout")!.Value);
    }

    [Fact] [Trait("Category", "Integration")]
    public void MissingSetting_UsesDefaultWithoutWritingFallback()
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        Assert.Equal("default", workspace.DashboardLayout); workspace.RefreshPersistedData();
        using var check = fixture.CreateDbContext(); Assert.Null(check.Settings.Find("dashboard_layout"));
        Assert.False(workspace.SetDashboardLayout("unknown")); Assert.Null(check.Settings.Find("dashboard_layout"));
    }

    [Fact] [Trait("Category", "Integration")]
    public void FailedSave_LeavesPreviousSelectionAndStoredValueIntact()
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        Assert.True(workspace.SetDashboardLayout("classic"));
        using var db = fixture.CreateDbContext();
        Assert.Equal("settings", db.Model.FindEntityType(typeof(AppSetting))!.GetTableName());
        db.Database.ExecuteSqlRaw("CREATE TRIGGER block_layout BEFORE UPDATE ON settings WHEN OLD.Key = 'dashboard_layout' BEGIN SELECT RAISE(ABORT, 'Injected settings failure'); END");
        Assert.False(workspace.SetDashboardLayout("bento")); Assert.Equal("classic", workspace.DashboardLayout);
        Assert.Equal("classic", db.Settings.AsNoTracking().Single(item => item.Key == "dashboard_layout").Value);
        Assert.Contains("could not save", workspace.Status);
    }
}

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class DashboardParityTests(HeadlessFixture headless)
{
    [Theory] [InlineData(1144)] [InlineData(700)] [InlineData(480)]
    public Task DefaultReference_SectionsActionsAndResponsiveBounds(int width) => headless.Run(() =>
    {
        var calls = new List<string>();
        var tiles = new[] { Tile("Customers", "100"), Tile("Products", "100"), Tile("Total Invoices", "7"), Tile("Revenue Collected", "Rs. 31.8K"), Tile("Outstanding", "Rs. 1012.44") };
        var recent = Enumerable.Range(0, 5).Select(index => new DashboardInvoiceModel
        {
            Number = index + 1, InvoiceTitle = "Invoice #" + (7 - index).ToString("D8"), CustomerName = index == 0 ? "Aditya Sharma" : "Akash Thakur", RawDate = "2026-09-18",
            TotalText = index == 0 ? "Rs. 919.22" : "Rs. 9440.00", Status = index == 0 ? "Unpaid" : "Paid",
            StatusBrush = Avalonia.Media.Brushes.Red, StatusBackground = Avalonia.Media.Brush.Parse("#FFEBEE"),
            ViewCommand = Command("View"), EditCommand = Command("Edit"), CloneCommand = Command("Clone"), PdfCommand = Command("Preview PDF"), DownloadCommand = Command("Download PDF"),
            PrintCommand = Command("Print"), PaymentCommand = Command("Record Payment"), DeleteCommand = Command("Delete")
        });
        var model = new DashboardPageModel(tiles, recent, [], [], [], "Rs. 31.8K", "Rs. 1012.44", "1", "Backpack", "admin", () => calls.Add("Refresh"),
            outOfStockProducts: [new("Backpack", "Product", "Rs.1299.00", "Stock: 0", Command("Restock"))], defaultCollectedText: "Rs. 31797.46", today: new DateTime(2026, 10, 8));
        var view = new DashboardPageView(model); var window = new Window { Content = view, Width = width, Height = 697 };
        try
        {
            window.Show(); Settle(window);
            Assert.Equal("Default", model.Layout); Assert.Equal(width, model.ViewportWidth, 0);
            var content = Assert.Single(view.GetVisualDescendants().OfType<DashboardDefaultView>());
            foreach (var name in new[] { "DefaultWelcomeBanner", "DefaultSummaryCards", "DefaultOutOfStockSection", "DefaultRecentDocumentsSection" })
                Assert.Contains(content.GetVisualDescendants().OfType<Control>(), control => control.Name == name && control.IsEffectivelyVisible);
            Assert.Equal(5, model.DefaultTiles.Count); Assert.Equal("Rs. 31797.46", model.DefaultTiles[3].Value);
            Assert.Equal(new[] { "Customers", "Products", "Invoices", "Revenue Collected", "Outstanding" }, model.DefaultTiles.Select(tile => tile.Label));
            Assert.Equal("Thursday", model.DayText); Assert.Equal("Oct 8, 2026", model.DateText);
            var cards = content.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("default-document-card")).ToArray(); Assert.Equal(5, cards.Length);
            var first = cards[0]; var actions = first.GetVisualDescendants().OfType<Button>().ToArray(); Assert.Equal(8, actions.Length);
            Assert.All(actions, button => { Assert.NotNull(ToolTip.GetTip(button)); Assert.Equal(42, button.Bounds.Width, 0); Assert.Equal(42, button.Bounds.Height, 0); });
            foreach (var action in actions) action.Command!.Execute(null);
            Assert.Equal(new[] { "View", "Edit", "Clone", "Preview PDF", "Download PDF", "Print", "Record Payment", "Delete" }, calls);
            foreach (var button in first.GetVisualDescendants().OfType<Button>())
            {
                var point = button.TranslatePoint(default, first)!.Value;
                Assert.True(point.X >= -1 && point.X + button.Bounds.Width <= first.Bounds.Width + 1, $"Action overflow at {width}: {point.X}/{first.Bounds.Width}");
            }
            var stocks = content.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button)?.ToString() == "Restock"); stocks.Command!.Execute(null); Assert.Equal("Restock", calls.Last());
            Capture(window, "default-" + width);
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First(item => item.GetVisualDescendants().Contains(content));
            scroll.Offset = new Vector(0, 500); Settle(window); Capture(window, "documents-" + width);
        }
        finally { window.Close(); }
        CommunityToolkit.Mvvm.Input.IRelayCommand Command(string name) => new CommunityToolkit.Mvvm.Input.RelayCommand(() => calls.Add(name));
        static DashboardTileModel Tile(string label, string value)
        {
            var (icon, color) = label switch { "Customers" => ("people", "#1976D2"), "Products" => ("inventory_2", "#2E7D32"), "Total Invoices" => ("receipt_long", "#F97316"), "Revenue Collected" => ("account_balance_wallet", "#8A2BE2"), _ => ("hourglass_top", "#D32F2F") };
            var accent = Avalonia.Media.Brush.Parse(color);
            return new(label, value, icon, accent, new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color), .12));
        }
    });

    [Theory] [InlineData("classic", "Classic")] [InlineData("bento", "Bento")] [InlineData("simple", "Simple")]
    public Task ActualDashboard_ReloadsSavedLayoutAndRefreshKeepsSelection(string saved, string display) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel(); Assert.True(workspace.SetDashboardLayout(saved));
        using (var db = fixture.CreateDbContext())
        {
            db.Users.Single(user => user.Username == "admin").PasswordChanged = true;
            db.SaveChanges();
        }
        var reloaded = fixture.CreateModel();
        reloaded.NavigateCommand.Execute("Dashboard");
        var window = new MainWindow { DataContext = reloaded, Width = 1366, Height = 768 };
        try
        {
            window.Show(); Settle(window);
            var view = window.GetVisualDescendants().OfType<DashboardPageView>().Single(); var state = Assert.IsType<DashboardPageModel>(view.DataContext);
            Assert.Equal(display, state.Layout); Assert.False(state.ShowDefaultFeed);
            state.RefreshCommand.Execute(null); Settle(window);
            state = Assert.IsType<DashboardPageModel>(window.GetVisualDescendants().OfType<DashboardPageView>().Single().DataContext);
            Assert.Equal(display, state.Layout);
            state.SelectLayoutCommand.Execute("Default"); Assert.Equal("Default", state.Layout);
            Assert.Equal("default", fixture.CreateModel().DashboardLayout);
        }
        finally { window.Close(); }
    });

    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "LedgerNest.CSharp.sln"))) root = root.Parent;
        Assert.NotNull(root); var output = Path.Combine(root!.FullName, "artifacts", "dashboard-reference-ui"); Directory.CreateDirectory(output);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame!.Save(Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
