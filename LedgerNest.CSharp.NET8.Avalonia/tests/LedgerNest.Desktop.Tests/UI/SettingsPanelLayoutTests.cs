using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class SettingsPanelLayoutTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(640, false)]
    [InlineData(1200, true)]
    public Task SettingsPanelsStackWithoutHorizontalOverflow(int width, bool dark) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var codes = new MasterCodeSettingsView { DataContext = fixture.CreateModel().CreateMasterCodeSettings() };
        var model = new SettingsPageModel(
            new[] { new SettingsTabModel("License", "lock"), new SettingsTabModel("Software Info", "info"), new SettingsTabModel("Codes", "settings") },
            "License", name => name == "License" ? new LicenseSettingsPageView() :
                name == "Software Info" ? new SoftwareInformationView() : codes, _ => { });
        var view = new SettingsPageView(model);
        var window = new Window { Content = view, Width = width, Height = 700,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show();
            foreach (var name in new[] { "License", "Software Info" })
            {
                model.SelectTabCommand.Execute(name); Settle(window);
                var page = model.CurrentContent;
                var scroll = page.GetVisualDescendants().OfType<ScrollViewer>().First();
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
                var cards = page.GetVisualDescendants().OfType<Border>().Where(c => c.Classes.Contains("settings-column")).ToArray();
                Assert.True(cards.Length >= 3);
                for (var index = 1; index < cards.Length; index++)
                {
                    var previous = cards[index - 1].TranslatePoint(default, page)!.Value;
                    var current = cards[index].TranslatePoint(default, page)!.Value;
                    Assert.Equal(previous.X, current.X);
                    Assert.True(current.Y >= previous.Y + cards[index - 1].Bounds.Height);
                }
            }
            model.SelectTabCommand.Execute("Codes"); Settle(window);
            foreach (var numeric in codes.GetVisualDescendants().OfType<NumericUpDown>())
            {
                Assert.Equal(36, numeric.Bounds.Height);
                var editor = numeric.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.True(editor.Bounds.Height <= numeric.Bounds.Height);
            }
        }
        finally { window.Close(); }
    });

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    }
}
