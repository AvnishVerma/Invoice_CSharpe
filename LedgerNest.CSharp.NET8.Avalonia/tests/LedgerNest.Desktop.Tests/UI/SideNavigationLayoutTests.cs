using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class SideNavigationLayoutTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(1200, false)]
    [InlineData(640, true)]
    public Task SettingsNavigationIsVerticalAndSelectionStillLoadsContent(int width, bool dark) => headless.Run(() =>
    {
        var names = new[] { "Company Info", "Backup", "Users", "Permissions", "PDF Settings", "Invoice Settings", "Product Details", "Customize", "Accessibility", "License", "Software Info", "General", "Other", "More" };
        var selected = "";
        var model = new SettingsPageModel(names.Select(name => new SettingsTabModel(name, "settings")), names[0],
            name => new TextBlock { Text = name }, name => selected = name);
        var view = new SettingsPageView(model);
        var window = new Window { Content = view, Width = width, Height = 600, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Settle(window);
            CheckRail(view, "SettingsNavigation", "SettingsNavigationScroll", "SettingsContent", width);
            var buttons = view.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("nav")).ToArray();
            Assert.Equal(names.Length, buttons.Length);
            Assert.All(buttons, button => Assert.True(button.Bounds.Height >= 48));
            Assert.True(buttons[1].TranslatePoint(default, view)!.Value.Y > buttons[0].TranslatePoint(default, view)!.Value.Y);
            model.SelectTabCommand.Execute("PDF Settings"); Settle(window);
            Assert.Equal("PDF Settings", selected);
            Assert.Equal("PDF Settings", Assert.IsType<TextBlock>(model.CurrentContent).Text);
            Assert.Single(model.Tabs, tab => tab.IsSelected);
            Capture(window, $"settings-left-{width}");
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(1200, false)]
    [InlineData(640, true)]
    public Task ReportsNavigationAndExistingFiltersStayInLeftRail(int width, bool dark) => headless.Run(() =>
    {
        var names = new[] { "Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report", "Inventory" };
        var selected = "";
        var model = new ReportsPageModel(names, names[0], name => new TextBlock { Text = name }, name => selected = name);
        var view = new ReportsPageView(model);
        var window = new Window { Content = view, Width = width, Height = 600, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Settle(window);
            CheckRail(view, "ReportsNavigationRail", "ReportsNavigationScroll", "ReportContent", width);
            var list = view.FindControl<ListBox>("ReportNavigation")!;
            var items = list.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
            Assert.Equal(names.Length, items.Length);
            Assert.True(items[1].TranslatePoint(default, view)!.Value.Y > items[0].TranslatePoint(default, view)!.Value.Y);
            Assert.All(items, item => Assert.True(item.Bounds.Height >= 48));
            Assert.Equal(9, view.GetVisualDescendants().OfType<RadioButton>().Count());
            model.SelectReportCommand.Execute("Inventory"); Settle(window);
            Assert.Equal("Inventory", selected); Assert.Equal("Inventory", model.SelectedReport);
            model.RefreshCommand.Execute(null); Settle(window);
            Assert.Equal("Inventory", Assert.IsType<TextBlock>(model.CurrentContent).Text);
            Capture(window, $"reports-left-{width}");
        }
        finally { window.Close(); }
    });

    private static void CheckRail(Control view, string railName, string scrollName, string contentName, int width)
    {
        var rail = view.FindControl<Border>(railName)!;
        var scroll = view.FindControl<ScrollViewer>(scrollName)!;
        var content = view.FindControl<ContentControl>(contentName)!;
        Assert.Equal(width <= 700 ? 156 : 200, rail.Bounds.Width);
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.True(content.TranslatePoint(default, view)!.Value.X >= rail.Bounds.Right);
        Assert.True(content.Bounds.Width > 0);
    }

    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "side-navigation");
        Directory.CreateDirectory(output);
        frame!.Save(Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
