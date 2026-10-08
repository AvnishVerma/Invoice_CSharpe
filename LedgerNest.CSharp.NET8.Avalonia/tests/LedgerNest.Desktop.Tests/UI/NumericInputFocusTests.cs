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
[Trait("Category", "UI")]
public sealed class NumericInputFocusTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NumericEditor_HasOnlyOuterOutlineWhenFocused(bool dark) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var view = new MasterCodeSettingsView { DataContext = fixture.CreateModel().CreateMasterCodeSettings() };
        var window = new Window { Content = view, Width = 600, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var numeric = view.GetVisualDescendants().OfType<NumericUpDown>().First();
            var input = numeric.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.True(input.Focus()); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(input.IsFocused);
            Assert.Equal(new Thickness(0), input.BorderThickness);
            Assert.Null(input.FocusAdorner);
            var innerBorders = input.GetVisualDescendants().OfType<Border>().ToArray();
            Assert.NotEmpty(innerBorders);
            Assert.All(innerBorders, border => Assert.Equal(new Thickness(0), border.BorderThickness));
            Assert.Contains(numeric.GetVisualDescendants().OfType<Border>(), item => !input.GetVisualDescendants().Contains(item) && item.BorderThickness != new Thickness(0));
            input.Text = "25"; Dispatcher.UIThread.RunJobs();
            Assert.Equal("25", input.Text);
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "LedgerNest.CSharp.sln"))) root = root.Parent;
            Assert.NotNull(root);
            var output = Path.Combine(root!.FullName, "artifacts", "numeric-focus-ui"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            using (var frame = window.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                frame!.Save(Path.Combine(output, dark ? "focused-dark.png" : "focused-light.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            var standalone = view.GetVisualDescendants().OfType<TextBox>().First(item => !numeric.GetVisualDescendants().Contains(item) && item.Name != "PART_TextBox");
            Assert.True(standalone.Focus()); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Contains(standalone.GetVisualDescendants().OfType<Border>(), border => border.BorderThickness != new Thickness(0));
        }
        finally { window.Close(); }
    });
}
