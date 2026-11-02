using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class CommunicationSettingsUiTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(640, false)] [InlineData(1200, true)]
    public Task ConfigurationUsesSettingsShellWithoutOverflowOrExposingSavedSecrets(int width, bool dark) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        var config = new CommunicationConfiguration(); config.For(DeliveryChannel.Telegram).Secret = "123:saved_token";
        workspace.CommunicationStore.SaveConfiguration(config);
        var state = new CommunicationSettingsViewModel(workspace);
        var shell = new SettingsPageView(new SettingsPageModel([new SettingsTabModel("Communication", "email")], "Communication",
            _ => new CommunicationSettingsView { DataContext = state }, _ => { }));
        var window = new Window { Content = shell, Width = width, Height = 768, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(3, state.Channels.Count);
            Assert.Equal(6, state.Sections.Count);
            Assert.True(state.Channels.Single(c => c.Title == "Email").IsSelected);
            foreach (var section in state.Sections)
            {
                state.SelectSectionCommand.Execute(section.Label);
                Assert.Single(state.Sections, s => s.IsSelected);
                Assert.Equal(section.Label == "Templates", state.ShowTemplates);
                Assert.Equal(section.Label == "Customer Consent", state.ShowConsent);
                Assert.Equal(section.Label == "History", state.ShowHistory);
                Assert.All(state.Channels, c => Assert.Equal(c.Title == section.Label, c.IsSelected));
            }
            state.SelectSectionCommand.Execute("Email");
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var page = shell.GetVisualDescendants().OfType<CommunicationSettingsView>().Single();
            var scroll = page.GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            foreach (var channel in state.Channels)
            {
                state.SelectSectionCommand.Execute(channel.Title);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var secrets = page.GetVisualDescendants().OfType<TextBox>().Where(t => t.PasswordChar != '\0' && t.IsEffectivelyVisible).ToArray();
                Assert.Single(secrets); Assert.True(string.IsNullOrEmpty(secrets[0].Text));
                Assert.All(page.GetVisualDescendants().OfType<ScrollViewer>(), s =>
                    Assert.True(s.Extent.Width <= s.Viewport.Width + 1));
            }
            state.SelectSectionCommand.Execute("Email");
            state.SaveCommand.Execute(null);
            Assert.Contains("saved", state.Status, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("123:saved_token", workspace.CommunicationStore.LoadConfiguration().For(DeliveryChannel.Telegram).Secret);
            Assert.True(state.Channels.Single(c => c.Title == "Email").IsSelected);
            var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "communication-preview"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame!.Save(Path.Combine(output, $"communication-{width}-{(dark ? "dark" : "light")}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { window.Close(); }
    });
    [Fact]
    public Task UnauthorizedSessionCannotConfigureCommunication() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel(false);
        Assert.Throws<UnauthorizedAccessException>(() => new CommunicationSettingsViewModel(workspace));
    });
}
