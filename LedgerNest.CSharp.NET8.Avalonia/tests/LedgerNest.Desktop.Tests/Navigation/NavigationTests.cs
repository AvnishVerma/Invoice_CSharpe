using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Navigation;

[Trait("Category", "Integration")]
public sealed class NavigationTests
{
    private static readonly (string Route, string Resource)[] Routes =
    [
        ("Dashboard", "Dashboard"), ("Invoices", "Invoice"), ("New Invoice", "Invoice"), ("Receipts", "Invoice"),
        ("Quotations", "Quotation"), ("Customers", "Customer"), ("Products", "Product"), ("Units", "Unit"),
        ("Prices", "Price"), ("Inventory", "Inventory"), ("Reports", "Reports"), ("Settings", "Settings")
    ];
    public static IEnumerable<object[]> AccessCases => Routes.SelectMany(route => new[] { false, true }
        .Select(allowed => new object[] { route.Route, route.Resource, allowed }));

    [Theory]
    [MemberData(nameof(AccessCases))]
    public async Task Navigate_ViewPermission_ControlsMenuAndDirectAccess(string route, string resource, bool allowed)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        TestData.CreateUser(model, "sales", "Sales");
        TestData.SetPermissions(fixture, "Sales", resource, allowed ? ["View"] : []);
        Assert.True(model.SignIn("sales", TestData.Password));
        model.Title = "sentinel";
        Assert.Equal(allowed, model.VisibleRoutes.Contains(route));
        Assert.Equal(allowed, model.CanNavigate(route));
        model.NavigateCommand.Execute(route);
        Assert.Equal(allowed ? route : "sentinel", model.Title);
        if (!allowed) Assert.Contains("Access denied", model.Status);
    }

    [Fact]
    public async Task Navigate_AddWithoutView_DoesNotGrantScreenAccess()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        TestData.CreateUser(model, "sales", "Sales");
        TestData.SetPermissions(fixture, "Sales", "Invoice", "Add");
        model.SignIn("sales", TestData.Password);
        Assert.False(model.CanNavigate("Invoices"));
        Assert.DoesNotContain("Invoices", model.VisibleRoutes);
    }

    [Fact]
    public void Navigate_UnknownRoute_DoesNotChangeScreen()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var title = model.Title;
        model.NavigateCommand.Execute("unknown://route");
        Assert.Equal(title, model.Title);
        Assert.False(model.CanNavigate("unknown://route"));
    }
}
