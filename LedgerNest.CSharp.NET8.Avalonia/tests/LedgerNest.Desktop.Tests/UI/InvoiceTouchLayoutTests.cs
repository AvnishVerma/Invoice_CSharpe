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
            var tabs=shell.GetVisualDescendants().OfType<TabControl>().Single(control=>control.Name=="InvoiceDetailTabs");
            Assert.Equal(5,tabs.ItemCount);
            Assert.All(tabs.Items.Cast<TabItem>(),tab=>Assert.True(tab.Bounds.Height>=48));
            var customer=shell.GetVisualDescendants().OfType<InvoiceCustomerPanelView>().Single();
            Assert.Contains(tabs,customer.GetVisualAncestors());
            AssertTabFormat(customer);
            foreach(var tab in tabs.Items.Cast<TabItem>())
                Assert.True(tab.TranslatePoint(default,tabs)!.Value.Y+tab.Bounds.Height <= customer.TranslatePoint(default,tabs)!.Value.Y+1);

            foreach(var action in customer.GetVisualDescendants().OfType<ActionButtonView>().Where(control=>control.ActionLabel is "Select customer" or "Create customer"))
            {
                var location=action.TranslatePoint(default,customer)!.Value;
                Assert.True(action.IsEffectivelyVisible);
                Assert.InRange(location.Y,0,customer.Bounds.Height);
                Assert.InRange(location.Y+action.Bounds.Height,0,customer.Bounds.Height+1);
                Assert.InRange(action.TranslatePoint(default,tabs)!.Value.Y+action.Bounds.Height,0,tabs.Bounds.Height+1);
            }
            tabs.SelectedIndex=1; Settle(window);
            AssertTabFormat(Assert.Single(shell.GetVisualDescendants().OfType<InvoiceDocumentDetailsPanelView>()));
            for(var index=2;index<5;index++)
            {
                tabs.SelectedIndex=index; Settle(window);
                var options=shell.GetVisualDescendants().OfType<InvoiceOptionsPanelView>().Single();
                AssertTabFormat(options);
                if(index==2)
                {
                    var state=Assert.IsType<InvoiceOptionsViewModel>(options.DataContext);
                    state.AddCostCommand.Execute(null); Settle(window);
                    Assert.Single(state.Costs);
                }
                var activeClass=index==2?"charges-section":index==3?"adjustments-section":"tax-section";
                Assert.Contains(options.GetVisualDescendants().OfType<Control>(),control=>control.Classes.Contains(activeClass)&&control.IsEffectivelyVisible);
                Assert.DoesNotContain(options.GetVisualDescendants().OfType<Control>(),control=>control.Classes.Any(name=>name is "charges-section" or "adjustments-section" or "tax-section")&&!control.Classes.Contains(activeClass)&&control.IsEffectivelyVisible);
            }
            tabs.SelectedIndex=2; Settle(window);
            var restored=Assert.IsType<InvoiceOptionsViewModel>(shell.GetVisualDescendants().OfType<InvoiceOptionsPanelView>().Single().DataContext);
            Assert.Single(restored.Costs);
            restored.Costs[0].RemoveCommand.Execute(null); Settle(window);
            Assert.Empty(restored.Costs);
            tabs.SelectedIndex=0; Settle(window);
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
            var narrowScroll=shell.GetVisualDescendants().OfType<ScrollViewer>().SingleOrDefault(control=>control.Name=="NarrowWorkspaceScroll");
            if(narrowScroll!=null) { narrowScroll.Offset=default; Settle(window); }
            var output=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","invoice-touch"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
            using var frame=window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame!.Save(Path.Combine(output,$"invoice-{width}-{(dark?"dark":"light")}.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { model.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing); model.MarkInvoiceFormClean(); window.Close(); }
    });
    private static void AssertTabFormat(Control view)
    {
        var card=Assert.Single(view.GetVisualDescendants().OfType<Border>(),control=>control.Classes.Contains("invoice-tab-card"));
        Assert.Equal(new Thickness(16),card.Padding);
        Assert.Equal(new CornerRadius(16),card.CornerRadius);
        Assert.Equal(new Thickness(1),card.BorderThickness);
        Assert.All(view.GetVisualDescendants().OfType<TextBlock>().Where(control=>control.Classes.Contains("invoice-tab-title")),title=>Assert.Equal(16,title.FontSize));
        Assert.All(view.GetVisualDescendants().OfType<StackPanel>().Where(control=>control.Classes.Contains("invoice-tab-content")),panel=>Assert.Equal(12,panel.Spacing));
    }
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
