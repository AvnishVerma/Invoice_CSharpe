using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Views;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class InvoiceOptionsUiParityTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(360)]
    [InlineData(600)]
    public Task Options_ActualAxaml_DynamicRowsAndSignedInput(int width) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        var view = new InvoiceOptionsPanelView(model);
        var window = new Window { Content = view, Width = width, Height = 900 };
        try
        {
            window.Show(); Settle(window);
            var outer = view.GetVisualDescendants().OfType<Expander>().Single(control => control.Header?.ToString() == "Discount & additional charges");
            outer.IsExpanded = true; Settle(window);
            var adjustments = view.GetVisualDescendants().OfType<Expander>().Single(control => control.Header?.ToString() == "Charges and Adjustments");
            adjustments.IsExpanded = true; Settle(window);
            var add = view.GetVisualDescendants().OfType<ActionButtonView>().Single(control => control.ActionLabel == "Add Cost");
            add.Command!.Execute(null); Settle(window); Assert.Single(model.AdditionalCosts);
            var amount = view.GetVisualDescendants().OfType<TextBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Amount");
            amount.Text = "-15"; Settle(window); Assert.Equal(-15m, model.AdditionalCosts[0][1].Number); Assert.True(model.AdditionalCosts[0][1].Validate());
            var remove = view.GetVisualDescendants().OfType<Button>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Remove adjustment");
            Assert.Equal("Remove adjustment", ToolTip.GetTip(remove)); remove.Command!.Execute(null); Settle(window);
            Assert.Empty(model.AdditionalCosts); Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBox>(), control => Avalonia.Automation.AutomationProperties.GetName(control) == "Amount");
            model.AdditionalCosts.Add(InvoiceOptionsViewModel.CreateCost("Reloaded", -3)); Settle(window);
            Assert.Single(view.GetVisualDescendants().OfType<TextBox>(), control => Avalonia.Automation.AutomationProperties.GetName(control) == "Amount");
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
