using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class UserManualUiTests(HeadlessFixture headless)
{
    [Fact]
    public Task SearchFindsInstructionsAndHandlesNoResults() => headless.Run(() =>
    {
        var model = new UserManualViewModel();
        Assert.True(model.Topics.Count >= 14);
        Assert.Equal(model.Topics.Count, model.Topics.Select(t => t.Title).Distinct().Count());
        Assert.All(model.Topics, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Summary));
            Assert.NotEmpty(t.Sections);
            Assert.All(t.Sections, s => { Assert.NotEmpty(s.Heading); Assert.NotEmpty(s.Text); });
        });
        model.Search = "STARTTLS";
        Assert.Contains(model.FilteredTopics, t => t.Title == "Email, WhatsApp and Telegram");
        Assert.Contains(model.SelectedTopic, model.FilteredTopics);
        model.Search = "no-matching-manual-topic-1234";
        Assert.True(model.HasNoResults); Assert.Null(model.SelectedTopic);
        model.Search = "";
        Assert.False(model.HasNoResults); Assert.Equal(model.Topics.Count, model.FilteredTopics.Count);
        Assert.NotNull(model.SelectedTopic);
    });

    [Theory]
    [InlineData(640, false)] [InlineData(1200, true)]
    public Task ManualUsesSettingsShellAndRendersEveryArticle(int width, bool dark) => headless.Run(() =>
    {
        var model = new UserManualViewModel();
        var shell = new SettingsPageView(new SettingsPageModel([new SettingsTabModel("User Manual", "description")], "User Manual",
            _ => new UserManualView { DataContext = model }, _ => { }));
        var window = new Window { Content = shell, Width = width, Height = 768, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var topic in model.Topics)
            {
                model.SelectedTopic = topic;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var article = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "ArticleScroll");
                Assert.True(article.Extent.Width <= article.Viewport.Width + 1);
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == topic.Title);
            }
            Assert.False(shell.DataContext is SettingsPageModel { HasSaveAction: true });
            var search = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.PlaceholderText == "Search manual");
            var searchPosition = search.TranslatePoint(default, window);
            Assert.NotNull(searchPosition);
            Assert.InRange(searchPosition!.Value.Y, 0, window.Height - search.Bounds.Height);
            model.Search = ""; model.SelectedTopic = model.Topics[0];
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "manual-preview"); Directory.CreateDirectory(output);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame!.Save(Path.Combine(output, $"manual-{width}-{(dark ? "dark" : "light")}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { window.Close(); }
    });
}
