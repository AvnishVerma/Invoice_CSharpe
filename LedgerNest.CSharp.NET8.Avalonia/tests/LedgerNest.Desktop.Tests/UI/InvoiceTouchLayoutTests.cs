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
public sealed class InvoiceTouchLayoutTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(1366,false)] [InlineData(1000,true)] [InlineData(800,false)] [InlineData(640,true)]
    public Task TouchEditorKeepsActionsVisibleAndItemInputsResponsive(int width,bool dark) => headless.Run(() =>
    {
        using var fixture=new TestDatabaseFixture(); var model=fixture.CreateModel(false);
        using(var db=fixture.CreateDbContext()) { db.Users.Single(user=>user.Username=="admin").PasswordChanged=true; db.SaveChanges(); }
        Assert.True(model.SignIn("admin","admin")); model.StartDocument("Invoice");
        var window=new MainWindow { DataContext=model, Width=width, Height=768, RequestedThemeVariant=dark?ThemeVariant.Dark:ThemeVariant.Light };
        try
        {
            window.Show(); Settle(window);
            var shell=window.GetVisualDescendants().OfType<InvoiceEditorShellView>().Single();
            var create=shell.GetVisualDescendants().OfType<Button>().Single(button=>button.Name=="CreateInvoiceButton");
            Assert.True(create.IsEffectivelyVisible); Assert.Equal(48,create.Bounds.Height);
            var point=create.TranslatePoint(default,shell)!.Value;
            Assert.InRange(point.X,0,shell.Bounds.Width); Assert.InRange(point.X+create.Bounds.Width,0,shell.Bounds.Width+1);
            Assert.InRange(point.Y+create.Bounds.Height,0,shell.Bounds.Height);
            Assert.Equal(4,shell.GetVisualDescendants().OfType<ActionButtonView>().Count(button=>button.ActionLabel is "View" or "Preview" or "Download" or "Print" && button.IsEffectivelyVisible));
            var select=shell.GetVisualDescendants().OfType<ActionButtonView>().Single(button=>button.ActionLabel=="Select customer");
            Assert.Equal(48,select.Bounds.Height);
            model.InvoiceOptions[3].Value="No Tax";
            model.Lines.Add(new InvoiceLineViewModel { Name="Touch-friendly product", Price=125, Quantity=2, TaxRate=0 }); Settle(window);
            var lines=shell.GetVisualDescendants().OfType<InvoiceLineItemsView>().Single();
            Assert.Equal(4,lines.GetVisualDescendants().OfType<NumericUpDown>().Count());
            var inputs=lines.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
            Assert.All(inputs, input=>Assert.Equal(36,input.Bounds.Height));
            var row=lines.GetVisualDescendants().OfType<Grid>().Single(panel=>panel.Classes.Contains("invoice-item-row"));
            Assert.Equal(7,row.ColumnDefinitions.Count);
            Assert.All(inputs, input=>Assert.Equal(inputs[0].TranslatePoint(default,row)!.Value.Y,input.TranslatePoint(default,row)!.Value.Y));
            Assert.All(inputs, input=>Assert.InRange(input.Bounds.Width,40,78));
            Assert.True(row.Bounds.Height<=48);
            Assert.Equal(ScrollBarVisibility.Auto,lines.GetVisualDescendants().OfType<ScrollViewer>().First().HorizontalScrollBarVisibility);
            Assert.Equal(250,model.Totals.Total);
            var output=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","invoice-touch"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
            using var frame=window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame!.Save(Path.Combine(output,$"invoice-{width}-{(dark?"dark":"light")}.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { model.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing); model.MarkInvoiceFormClean(); window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
