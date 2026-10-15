using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class DialogLayoutTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActionDialogsHaveOneActionAreaAndReadableRefundQuantity(bool dark) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        int invoiceId;
        using (var db = fixture.CreateDbContext())
        {
            var invoice = new LedgerNest.Domain.Invoice { InvoiceNumber = "00000004", CustomerName = "Customer", Items = [new LedgerNest.Domain.InvoiceItem { Description = "Annual Maintenance Contract", Quantity = 1, UnitPrice = 12000 }] };
            db.Invoices.Add(invoice); db.SaveChanges(); invoiceId = invoice.Id;
        }
        var record = new UiRecord { SourceId = invoiceId, Values = new Dictionary<string,string> { ["Name"] = "00000004", ["Customer"] = "Customer" } };
        var closed = 0;
        var refund = new InvoiceRefundViewModel(fixture,record,()=>"admin",()=>{},()=>closed++);
        var cancellation = new QuotationCancellationViewModel(workspace,record,()=>{},()=>closed++);
        foreach (var body in new Control[] { new InvoiceRefundView { DataContext = refund }, new QuotationCancellationView { DataContext = cancellation } })
        {
            var state = new OverlayDialogViewModel(body is InvoiceRefundView ? "Create Refund" : "Cancel Quotation",body,null,null,null,()=>closed++,false,false,false,760,800,800);
            Assert.False(state.HasFooter);
            var view = new OverlayDialogView { DataContext = state };
            var window = new Window { Content = view, Width = 800, Height = 800, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            try
            {
                window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.False(view.GetVisualDescendants().OfType<ContentControl>().Single(control=>control.Name=="FooterHost").IsEffectivelyVisible);
                Assert.Equal(20,view.GetVisualDescendants().OfType<TextBlock>().Single(text=>text.Name=="TitleText").FontSize);
                Assert.Equal(body is InvoiceRefundView ? 1 : 0, view.GetVisualDescendants().OfType<Button>().Count(button=>button.IsEffectivelyVisible && button.Content as string=="Close"));
                if(body is InvoiceRefundView)
                {
                    var quantity = body.GetVisualDescendants().OfType<NumericUpDown>().Single();
                    Assert.True(quantity.Bounds.Width >= 130);
                    quantity.Value = 1; Dispatcher.UIThread.RunJobs(); Assert.Equal(12000,refund.RefundTotal);
                }
                var output=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","dialog-layouts"); Directory.CreateDirectory(output);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                using var frame=window.CaptureRenderedFrame(); Assert.NotNull(frame);
                frame!.Save(Path.Combine(output,(body is InvoiceRefundView ? "refund" : "cancel")+(dark ? "-dark.png" : "-light.png")),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            finally { window.Close(); }
        }
    });
}
