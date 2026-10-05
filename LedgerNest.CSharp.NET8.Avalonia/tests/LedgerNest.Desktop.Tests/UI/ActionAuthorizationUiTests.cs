using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class ActionAuthorizationUiTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public Task Customer_ViewAndIndependentActions_ActualControlsMatchPermissions(bool add, bool update, bool delete) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext())
        {
            db.Roles.Add(new AppRole { Name = "Sales" });
            db.SaveChanges();
        }
        model.PermissionManagement.RefreshUsers();
        TestData.CreateUser(model, "sales", "Sales");
        var fields = FormCatalog.Customer();
        fields.Single(field => field.Label == "Name").Value = "Permission fixture";
        fields.Single(field => field.Label == "Phone").Value = "5550100";
        Assert.True(model.SaveRecord("Customer", fields));
        var actions = new List<string> { "View" };
        if (add) actions.Add("Add");
        if (update) actions.Add("Update");
        if (delete) actions.Add("Delete");
        TestData.SetPermissions(fixture, "Sales", "Customer", actions.ToArray());
        Assert.True(model.SignIn("sales", TestData.Password));
        model.NavigateCommand.Execute("Customers");
        var window = new MainWindow { DataContext = model, Width = 1440, Height = 900 };
        try
        {
            window.Show();
            Settle(window);
            bool Available(string tooltip) => window.GetVisualDescendants().OfType<Button>()
                .Any(button => ToolTip.GetTip(button)?.ToString() == tooltip && button.IsEffectivelyVisible && button.IsEnabled);
            Assert.True(Available("Refresh"));
            Assert.True(Available("View"));
            Assert.Equal(add, Available("New Customer"));
            Assert.Equal(update, Available("Edit"));
            Assert.Equal(delete, Available("Delete"));
            var refresh = window.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button)?.ToString() == "Refresh");
            Assert.Equal(refresh.Bounds.Height, refresh.Bounds.Width);
            Assert.Equal("Customers", model.Title);
            refresh.Command?.Execute(refresh.CommandParameter);
            Settle(window);
            Assert.Equal("Customers", model.Title);
            Assert.Single(model.Customers);
        }
        finally { window.Close(); }
    });

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
    }
}
