using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Companies;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class CompanyUiParityTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(800, 600)]
    [InlineData(1400, 900)]
    public Task Companies_ActualAxaml_BindsSelectionAndCommandsWithoutOverlappingHeader(double width, double height) => headless.Run(() =>
    {
        using var fixture = new CompanyFixture();
        var model = fixture.Model(fixture.Original, true);
        var manager = model.CompanyManagement!;
        var view = new CompanyManagementView { DataContext = manager };
        var window = new Window { Content = view, Width = width, Height = height };
        try
        {
            window.Show(); Settle(window);
            manager.NewCompanyName = "UI business";
            manager.CreateCompanyCommand.Execute(null);
            Settle(window);
            var combo = view.GetVisualDescendants().OfType<ComboBox>().Single();
            Assert.Equal("UI business", manager.SelectedCompany!.Name);
            Assert.Same(manager.SelectedCompany, combo.SelectedItem);
            Assert.True(manager.CanSwitch);
            var header = Assert.Single(view.GetVisualDescendants().OfType<PageHeaderView>());
            Assert.Equal("Companies", header.Title);
            Assert.True(header.IsEffectivelyVisible);
            var refresh = header.GetVisualDescendants().OfType<Button>().Single();
            Assert.Equal("Refresh", ToolTip.GetTip(refresh));
            Assert.Same(manager.RefreshCommand, refresh.Command);
            model.SignOut(); Settle(window);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && button.Content?.ToString() == "Create company");
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task StartupFailure_ActualAxaml_ShowsRecoveryActions() => headless.Run(() =>
    {
        var retries = 0;
        var model = new StartupFailureViewModel("Database could not open", () => retries++, () => { });
        var window = new StartupFailureWindow { DataContext = model };
        try
        {
            window.Show(); Settle(window);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Database could not open");
            var retry = window.GetVisualDescendants().OfType<Button>().Single(button => button.Content?.ToString() == "Retry");
            retry.Command!.Execute(null);
            Assert.Equal(1, retries);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Sidebar_ActualAxaml_UsesRegistryCollectionAndSharedSwitchCommand() => headless.Run(() =>
    {
        using var fixture = new CompanyFixture();
        var second = fixture.Registry.Create("B");
        var model = fixture.Model(fixture.Original, true);
        var view = new SidebarView(model, _ => { }, () => { }, model.SignOut);
        var window = new Window { Content = view, Width = 260, Height = 800 };
        try
        {
            window.Show(); Settle(window);
            var combo = Assert.Single(view.GetVisualDescendants().OfType<ComboBox>());
            Assert.Same(model.CompanyManagement!.Companies, combo.ItemsSource);
            combo.SelectedItem = model.CompanyManagement.Companies.Single(company => company.Id == second.Id);
            Settle(window);
            Assert.Equal(second.Id, model.CompanyManagement.SelectedCompany!.Id);
            var button = view.GetVisualDescendants().OfType<Button>().Single(item => Equals(ToolTip.GetTip(item), "Switch Company"));
            Assert.Same(model.CompanyManagement.SwitchCompanyCommand, button.Command);
            Assert.True(button.IsEnabled);
        }
        finally { window.Close(); }
    });

    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
