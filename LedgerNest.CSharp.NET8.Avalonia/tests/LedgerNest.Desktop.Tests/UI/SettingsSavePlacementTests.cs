using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class SettingsSavePlacementTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(1200)] [InlineData(700)]
    public Task SettingsUseOneFooterAndOriginalCommands(int width) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel(); Assert.True(model.SignIn("admin", "admin"));
        var owner = new MainWindow { DataContext = model };
        var createSettings = typeof(MainWindow).GetMethod("SettingsView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var page = (SettingsPageView)createSettings.Invoke(owner, null)!;
        var window = new Window { Content = page, Width = width, Height = 900 };
        try
        {
            window.Show(); Settle(window);
            var shell = window.GetVisualDescendants().OfType<SettingsPageView>().Single();
            var state = (SettingsPageModel)shell.DataContext!;
            foreach (var category in new[] { "Company Info", "Invoice Settings", "Product Details", "PDF Settings", "Permissions", "Accessibility", "Software Info" })
            {
                state.SelectedTab = category; Settle(window);
                Assert.NotNull(state.CurrentSaveSource);
                var save = shell.GetVisualDescendants().OfType<ActionButtonView>().Single(button => button.Classes.Contains("settings-save-primary"));
                Assert.True(save.IsEffectivelyVisible, category);
                Assert.False(state.CurrentSaveSource!.IsVisible);
                Assert.Same(state.CurrentSaveSource.Command, save.Command);
                Assert.Equal(48, save.Bounds.Height);
                var point = save.TranslatePoint(default, shell)!.Value;
                Assert.InRange(point.X + save.Bounds.Width, shell.Bounds.Width - 20, shell.Bounds.Width);
                Assert.InRange(point.Y + save.Bounds.Height, shell.Bounds.Height - 16, shell.Bounds.Height);
                Assert.Single(shell.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && (button.Classes.Contains("settings-save-source") || button.Classes.Contains("settings-save-primary")));
            }
            state.SelectedTab = "Backup"; Settle(window); Assert.False(state.HasSaveAction);
            Assert.DoesNotContain(shell.GetVisualDescendants().OfType<ActionButtonView>(), button => button.Classes.Contains("settings-save-primary") && button.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
