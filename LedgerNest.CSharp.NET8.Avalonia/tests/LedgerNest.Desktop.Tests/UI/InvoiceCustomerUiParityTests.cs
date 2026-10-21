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
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), control => control.Classes.Contains("customer-summary-value") && control.Text == "Customer");
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBox>(), control => control.IsEffectivelyVisible);
            var state = Assert.IsType<InvoiceCustomerPanelViewModel>(view.DataContext);
            state.EditDetailsCommand.Execute(null); Settle(window);
            var name = view.GetVisualDescendants().OfType<TextBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Name");
            Assert.True(name.IsReadOnly);
            var toggle = view.GetVisualDescendants().OfType<Button>().Single(control => ToolTip.GetTip(control)?.ToString() == "Unlock customer fields");
            toggle.Command!.Execute(null); Settle(window); Assert.False(name.IsReadOnly);
            state.DetailsExpanded = true; Settle(window);
            var address = view.GetVisualDescendants().OfType<TextBox>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Address");
            Assert.False(address.IsReadOnly); address.Text = "Invoice address"; Settle(window); Assert.Equal("Invoice address", model.InvoiceCustomer[5].Value);
            var refresh = view.GetVisualDescendants().OfType<Button>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Refresh customer");
            Assert.Equal("Refresh", ToolTip.GetTip(refresh)); refresh.Command!.Execute(null); Settle(window);
            Assert.True(name.IsReadOnly); Assert.True(state.DetailsExpanded); Assert.Equal("Master address", model.InvoiceCustomer[5].Value);
            var clear = view.GetVisualDescendants().OfType<Button>().Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Clear customer");
            clear.Command!.Execute(null); Settle(window); Assert.False(name.IsReadOnly); Assert.Null(model.SelectedInvoiceCustomerId); Assert.Empty(name.Text!);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    [Fact]
    public Task CreateCustomer_UsesExistingPersistenceAndSelectsNewCustomer() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel(false);
        using (var db = fixture.CreateDbContext()) { db.Users.Single(user => user.Username == "admin").PasswordChanged = true; db.SaveChanges(); }
        Assert.True(model.SignIn("admin", "admin")); model.StartDocument("Invoice");
        var window = new MainWindow { DataContext = model, Width = 1366, Height = 800 };
        try
        {
            window.Show(); Settle(window);
            var view = window.GetVisualDescendants().OfType<InvoiceCustomerPanelView>().Single();
            var state = Assert.IsType<InvoiceCustomerPanelViewModel>(view.DataContext);
            state.CreateCommand.Execute(null); Settle(window);
            var dialog = Assert.IsType<OverlayDialogViewModel>(model.CurrentOverlay);
            var form = Assert.IsType<FormFieldsViewModel>(dialog.Body);
            form.Fields[0].Field.Value = "Cancelled draft";
            dialog.CloseCommand.Execute(null); Settle(window);
            Assert.Empty(model.Customers); Assert.Null(model.SelectedInvoiceCustomerId);
            state.CreateCommand.Execute(null); Settle(window);
            dialog = Assert.IsType<OverlayDialogViewModel>(model.CurrentOverlay);
            form = Assert.IsType<FormFieldsViewModel>(dialog.Body);
            var save = Assert.IsType<DialogActions>(dialog.Footer).Items.Last().Command!;
            save.Execute(null); Assert.Same(dialog, model.CurrentOverlay); Assert.Empty(model.Customers);
            form.Fields[0].Field.Value = "Created from invoice";
            form.Fields[2].Field.Value = "9876543210";
            save.Execute(null); Settle(window);
            Assert.Null(model.CurrentOverlay);
            var created = Assert.Single(model.Customers);
            Assert.Equal(created.SourceId, model.SelectedInvoiceCustomerId);
            Assert.Equal("Created from invoice", model.InvoiceCustomer[0].Value);
            Assert.False(string.IsNullOrWhiteSpace(created["Customer ID"]));
            using var verify = fixture.CreateDbContext();
            Assert.Equal(created.SourceId, Assert.Single(verify.Customers).Id);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), control => control.Classes.Contains("customer-summary-value") && control.Text == "Created from invoice");
        }
        finally { model.MarkInvoiceFormClean(); window.Close(); }
    });
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
