using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class FloatingLabelTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData("form", false)] [InlineData("form", true)]
    [InlineData("invoice", false)] [InlineData("invoice", true)]
    [InlineData("product", false)] [InlineData("product", true)]
    [InlineData("company", false)] [InlineData("company", true)]
    public Task SharedCaptionMasksOutlineAndPreservesInputStates(string kind, bool dark) => headless.Run(() =>
    {
        var field = new FormField(kind == "invoice" ? "Invoice Prefix" : kind == "company" ? "Nombre del cliente y denominación comercial completa" : "Name", "") { Icon = "person" };
        Control view = kind switch
        {
            "invoice" => new InvoiceInputView { DataContext = new InvoiceInputViewModel(field, field.Label, "person") },
            "product" => new ProductInputView { DataContext = new ProductInputViewModel(field, "person") },
            "company" => new CompanyFieldView { DataContext = new CompanyFieldModel(field, "person") },
            _ => new FormFieldView { DataContext = new FormFieldViewModel(field) }
        };
        var other = new Button { Content = "Move focus" };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 20 };
        panel.Children.Add(view); panel.Children.Add(other);
        var window = new Window { Content = panel, Width = 500, Height = 240, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Settle(window);
            var input = view.GetVisualDescendants().OfType<TextBox>().Single();
            var caption = view.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("field-caption"));
            other.Focus(); Settle(window); Assert.False(caption.IsVisible);
            Assert.True(input.Focus()); Settle(window); Assert.True(caption.IsVisible);
            var renderedOutline = input.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_BorderElement");
            Assert.Equal(((ISolidColorBrush)caption.Background!).Color, ((ISolidColorBrush)renderedOutline.Background!).Color);
            Assert.All(input.GetVisualDescendants().OfType<TextBlock>(), watermark => Assert.Equal(0, watermark.Opacity));
            Assert.IsAssignableFrom<ISolidColorBrush>(caption.Background); Assert.Equal((byte)255, ((ISolidColorBrush)caption.Background!).Color.A);
            Assert.Equal(((ISolidColorBrush)input.Background!).Color, ((ISolidColorBrush)caption.Background!).Color);
            Assert.Equal(16, caption.Bounds.Height); Assert.True(caption.TranslatePoint(default,view)!.Value.Y >= 0);
            Assert.True(input.Padding.Left >= 32, "Leading icon must have reserved text padding");
            var inputY = input.TranslatePoint(default, view)!.Value.Y;
            var captionY = caption.TranslatePoint(default, view)!.Value.Y;
            Assert.InRange(inputY, captionY, captionY + caption.Bounds.Height);
            input.Text = "CUS-"; Settle(window); Assert.Equal("CUS-", field.Value);
            input.SelectionStart = 0; input.SelectionEnd = 3; Assert.Equal("CUS", input.SelectedText);
            field.Error = "Required"; Settle(window);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Required" && text.IsVisible);
            var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "floating-labels"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame!.Save(Path.Combine(output, kind + (dark ? "-dark.png" : "-light.png")), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            if (kind == "company")
            {
                window.Width = 180; Settle(window);
                Assert.True(caption.Bounds.Width <= input.Bounds.Width - 20);
                window.Width = 500; Settle(window);
            }
            other.Focus(); Settle(window); Assert.False(caption.IsVisible); Assert.Equal("CUS-", field.Value);
            field.IsReadOnly = true; Settle(window); Assert.True(input.IsReadOnly);
            view.IsEnabled = false; Settle(window); Assert.False(input.IsEffectivelyEnabled); Assert.False(caption.IsVisible);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
}
