using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Views;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class InvoiceCustomerUiParityTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(360)]
    [InlineData(900)]
    public Task CustomerPanel_ActualAxaml_LocksUnlocksRefreshesAndClears(int width) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        using (var db = fixture.CreateDbContext()) { db.Customers.Add(new Customer { Name = "Customer", Phone = "123", Address = "Master address" }); db.SaveChanges(); }
        var model = fixture.CreateModel(); var pickerRequests = 0;
        var view = new InvoiceCustomerPanelView(model, () => pickerRequests++);
        var window = new Window { Content = view, Width = width, Height = 800 };
        try
        {
            window.Show(); Settle(window);
            var select = view.GetVisualDescendants().OfType<ActionButtonView>().Single(button => button.ActionLabel == "Select customer");
            select.Command!.Execute(null); Assert.Equal(1, pickerRequests);
            Assert.True(model.SelectInvoiceCustomer(model.Customers.Single())); Settle(window);
            var name = view.GetVisualDescendants().OfType<TextBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Name");
            Assert.True(name.IsReadOnly);
            var toggle = view.GetVisualDescendants().OfType<Button>().Single(control => ToolTip.GetTip(control)?.ToString() == "Unlock customer fields");
            toggle.Command!.Execute(null); Settle(window); Assert.False(name.IsReadOnly);
            var state = Assert.IsType<InvoiceCustomerPanelViewModel>(view.DataContext); state.DetailsExpanded = true; Settle(window);
            var address = view.GetVisualDescendants().OfType<TextBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Address");
            Assert.False(address.IsReadOnly); address.Text = "Invoice address"; Settle(window); Assert.Equal("Invoice address", model.InvoiceCustomer[5].Value);
            var refresh = view.GetVisualDescendants().OfType<Button>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Refresh customer");
            Assert.Equal("Refresh", ToolTip.GetTip(refresh)); refresh.Command!.Execute(null); Settle(window);
            Assert.True(name.IsReadOnly); Assert.False(state.DetailsExpanded); Assert.Equal("Master address", model.InvoiceCustomer[5].Value);
            var clear = view.GetVisualDescendants().OfType<Button>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Clear customer");
            clear.Command!.Execute(null); Settle(window); Assert.False(name.IsReadOnly); Assert.Null(model.SelectedInvoiceCustomerId); Assert.Empty(name.Text!);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    [Fact]
    public Task CustomerChooser_ActualAxaml_FiltersAndSelectsExplicitId() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        using (var db = fixture.CreateDbContext()) { db.Customers.AddRange(new Customer { Name = "Same", Phone = "111" }, new Customer { Name = "Same", Phone = "222" }); db.SaveChanges(); }
        var model = fixture.CreateModel();
        var chooser = new CustomerChooserView(model.Customers, customer => Assert.True(model.SelectInvoiceCustomer(customer)));
        var window = new Window { Content = chooser, Width = 420, Height = 500 };
        try
        {
            window.Show(); Settle(window);
            var list = chooser.FindControl<ListBox>("CustomersList")!; Assert.Equal(2, list.ItemCount);
            chooser.FindControl<TextBox>("SearchBox")!.Text = "222"; Settle(window); Assert.Equal(1, list.ItemCount);
            list.SelectedIndex = 0; Settle(window);
            Assert.Equal(model.Customers.Single(customer => customer["Phone"] == "222").SourceId, model.SelectedInvoiceCustomerId);
            Assert.True(model.CustomerIdentityLocked); Assert.Equal("222", model.InvoiceCustomer[2].Value);
        }
        finally { window.Close(); }
    });
}
