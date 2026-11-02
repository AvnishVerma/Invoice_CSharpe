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
        Assert.True(model.Topics.Count >= 49);
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

    [Fact]
    public Task ManualCoversEveryWorkspaceRouteAndAdministrationProcess() => headless.Run(() =>
    {
        var model = new UserManualViewModel();
        var routeTopics = new Dictionary<string, string>
        {
            ["Dashboard"] = "Dashboard layouts and quick actions",
            ["New Invoice"] = "Create an invoice",
            ["Invoices"] = "Edit, clone and manage documents",
            ["Quotations"] = "Quotation lifecycle",
            ["Receipts"] = "Receipt lifecycle",
            ["Customers"] = "Customer CSV import",
            ["Products"] = "Product CSV import",
            ["Units"] = "Unit master",
            ["Prices"] = "Selling units and effective prices",
            ["Inventory"] = "Post and verify a stock adjustment",
            ["Reports"] = "Tax, customer and product reports",
            ["Settings"] = "Company, invoice and PDF settings"
        };
        Assert.All(MainWindowViewModel.Routes, route =>
        {
            Assert.True(routeTopics.ContainsKey(route), $"Add manual coverage for route {route}.");
            Assert.Contains(model.Topics, topic => topic.Title == routeTopics[route] && topic.Sections.Length >= 3);
        });
        string[] administration = ["First-time setup wizard", "Sign in, passwords and recovery", "Bank accounts and UPI QR",
            "Invoice custom fields", "PDF layout and printing", "Users and role administration", "Permissions by resource",
            "License activation", "Software updates and notifications", "Backup history and recovery checks",
            "Communication templates and customer consent", "Admin notification configuration", "Daily closing process"];
        Assert.All(administration, title => Assert.Contains(model.Topics, topic => topic.Title == title && topic.Sections.Length >= 3));
        model.Search = "permanently";
        Assert.Contains(model.FilteredTopics, topic => topic.Title == "Edit, clone and manage documents");
        model.Search = "500";
        Assert.Contains(model.FilteredTopics, topic => topic.Title == "Product CSV import");
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
