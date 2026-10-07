using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class InvoiceEditUiParityTests(HeadlessFixture headless)
{
    [Fact]
    public Task DocumentDetails_ActualAxaml_BindsOrderTimeAndHistoricalBankSelection() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel(); model.AddBankAccount();
        model.BankAccounts[0][2].Value = "123456789";
        var view = new InvoiceDocumentDetailsPanelView(model);
        var window = new Window { Content = view, Width = 400, Height = 800 };
        try
        {
            window.Show(); Settle(window);
            var time = view.GetVisualDescendants().OfType<TextBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Order Time");
            time.Text = "10:45"; Settle(window);
            Assert.Equal("10:45", model.OrderTime.Value);
            var bank = view.GetVisualDescendants().OfType<ComboBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Invoice bank account");
            bank.SelectedIndex = 1; Settle(window);
            Assert.Equal("123456789", model.SelectedInvoiceBank!.Account!.AccountNumber);
            Assert.Contains(view.GetVisualDescendants().OfType<Expander>(), control => control.Header?.ToString() == "Document title & numbering");
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
