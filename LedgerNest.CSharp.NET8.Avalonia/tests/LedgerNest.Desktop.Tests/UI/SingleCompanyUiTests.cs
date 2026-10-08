using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Companies;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using System.Reflection;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class SingleCompanyUiTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(800)]
    [InlineData(1400)]
    public Task SettingsAndSidebar_DoNotExposeCompanyManagement(int width) => headless.Run(() =>
    {
        using var fixture = new CompanyFixture();
        fixture.Registry.Create("Hidden second business");
        var model = fixture.Model(fixture.Original, true);
        var owner = new MainWindow { DataContext = model };
        var content = (Control)typeof(MainWindow).GetMethod("SettingsView", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, null)!;
        var window = new Window { Content = content, Width = width, Height = 800 };
        try
        {
            window.Show(); Settle(window);
            Assert.DoesNotContain(content.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "Companies");
            var sidebar = new SidebarView(model, _ => { }, () => { }, model.SignOut);
            window.Content = sidebar; Settle(window);
            Assert.Empty(sidebar.GetVisualDescendants().OfType<ComboBox>());
            Assert.DoesNotContain(sidebar.GetVisualDescendants().OfType<Button>(), item => ToolTip.GetTip(item)?.ToString()?.Contains("Company") == true);
            Assert.DoesNotContain(sidebar.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "Hidden second business");
            Assert.Equal(2, fixture.Registry.Read().Companies.Length);
            Assert.NotNull(model.CompanyContext);
            Assert.NotNull(model.CompanyManagement); // Internal architecture is retained.
            var staleRoute = (Control)typeof(MainWindow).GetMethod("SettingsContent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, ["Companies"])!;
            window.Content = staleRoute; Settle(window);
            Assert.DoesNotContain(staleRoute.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "Companies");
        }
        finally { window.Close(); owner.Close(); }
    });

    [Fact]
    public Task StartupFailure_ActualAxaml_ShowsRecoveryActions() => headless.Run(() =>
    {
        var retries = 0;
        var window = new StartupFailureWindow { DataContext = new StartupFailureViewModel("Database could not open", () => retries++, () => { }) };
        try
        {
            window.Show(); Settle(window);
            var retry = window.GetVisualDescendants().OfType<Button>().Single(button => button.Content?.ToString() == "Retry");
            retry.Command!.Execute(null); Assert.Equal(1, retries);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
