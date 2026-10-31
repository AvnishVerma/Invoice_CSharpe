using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class CompactNumericInputTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData("Barcodes", false)] [InlineData("Barcodes", true)]
    [InlineData("Inventory", false)] [InlineData("Inventory", true)]
    [InlineData("Prices", false)] [InlineData("Prices", true)]
    public Task NumericInputsUseCompactSharedStyleAndRetainEditing(string screen, bool dark) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        Control view = screen switch
        {
            "Barcodes" => new BarcodeReportView(new BarcodeReportViewModel(
                [new UiRecord { Values = new Dictionary<string,string> { ["Name"]="Test product", ["Product ID"]="PROD00001" } }], _ => Task.CompletedTask)),
            "Inventory" => new InventoryView { DataContext = new InventoryViewModel(fixture, () => "admin", () => "Admin") },
            _ => new ProductPriceMasterView { DataContext = new ProductPriceMasterViewModel(fixture, () => "admin", () => "Admin") }
        };
        var window = new Window { Content = view, Width = 1200, Height = 850, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Settle(window);
            var inputs = view.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
            Assert.NotEmpty(inputs);
            foreach (var input in inputs)
            {
                Assert.Equal(36, input.Bounds.Height);
                Assert.Equal(new CornerRadius(10), input.CornerRadius);
                Assert.Equal(15, input.FontSize);
                Assert.Equal(VerticalAlignment.Center, input.VerticalContentAlignment);
                var editor = Assert.Single(input.GetVisualDescendants().OfType<TextBox>());
                Assert.Equal(new Thickness(0), editor.BorderThickness);
                Assert.True(editor.Bounds.Height <= 36, $"Inner editor is {editor.Bounds.Height} DIP high in {screen}");
                var buttons = input.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).ToArray();
                Assert.Equal(2, buttons.Length);
                Assert.All(buttons, button => Assert.InRange(button.Bounds.Width, 1, 24));
                input.Value = 2; Settle(window);
                Assert.Equal(2, input.Value);
                editor.Text = "3"; Settle(window);
                Assert.Equal(3, input.Value);
                Assert.True(editor.Focus()); Settle(window);
                Assert.Equal(new Thickness(0), editor.BorderThickness);
            }
            switch (view.DataContext)
            {
                case BarcodeReportViewModel barcode: Assert.All(barcode.VisibleRows, row => Assert.Equal(3, row.Quantity)); break;
                case InventoryViewModel inventory: Assert.Equal(3, inventory.Quantity); break;
                case ProductPriceMasterViewModel prices:
                    Assert.Equal(3, prices.ConversionFactor); Assert.Equal(3, prices.UnitSellingPrice); Assert.Equal(3, prices.EffectiveSellingPrice); break;
            }
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "compact-numeric");
            Directory.CreateDirectory(output);
            frame!.Save(Path.Combine(output, $"{screen}-{(dark ? "dark" : "light")}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
