using LedgerNest.Desktop.Tests.Fixtures;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LedgerNest.Desktop.Tests.UI;

public sealed class InvoiceNavigationGuardTests
{
    [Theory]
    [InlineData("Dashboard")] [InlineData("Invoices")] [InlineData("Quotations")]
    [InlineData("Receipts")] [InlineData("Customers")] [InlineData("Products")]
    [InlineData("Units")] [InlineData("Prices")] [InlineData("Inventory")]
    [InlineData("Reports")] [InlineData("Settings")]
    public void DirtyInvoice_AllRoutesKeepOrDiscardSafely(string destination)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.StartDocument("Invoice"); Assert.False(model.HasUnsavedInvoiceChanges);
        var requested = 0; model.InvoiceLeaveRequested += () => requested++;
        model.InvoiceOptions[2].Value = "Unsaved note";
        model.NavigateCommand.Execute(destination);
        Assert.Equal("New Invoice", model.Title); Assert.Equal(1, requested);
        Assert.False(model.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing));
        Assert.Equal("Unsaved note", model.InvoiceOptions[2].Value);
        model.NavigateCommand.Execute(destination);
        Assert.True(model.ResolveInvoiceLeave(InvoiceLeaveChoice.Discard));
        Assert.Equal(destination, model.Title); Assert.Empty(model.InvoiceOptions[2].Value);
        Assert.False(model.HasUnsavedInvoiceChanges);
    }

    [Fact]
    public void UntouchedInvoice_NavigatesImmediately()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.StartDocument("Invoice"); model.NavigateCommand.Execute("Dashboard");
        Assert.Equal("Dashboard", model.Title); Assert.False(model.HasPendingInvoiceNavigation);
    }

    [Fact]
    public void LogoutAndNewInvoice_UseTheSameGuard()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.StartDocument("Invoice"); model.InvoiceCustomer[0].Value = "Draft customer";
        model.SignOut(); Assert.Equal("admin", model.CurrentUsername);
        model.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing);
        model.StartDocument("Quotation"); Assert.Equal("Invoice", model.InvoiceDetails[0].Value);
        Assert.Equal("Draft customer", model.InvoiceCustomer[0].Value);
        model.ResolveInvoiceLeave(InvoiceLeaveChoice.Discard);
        Assert.Equal("Quotation", model.InvoiceDetails[0].Value); Assert.False(model.HasUnsavedInvoiceChanges);
        model.InvoiceOptions[2].Value = "Logout draft"; model.SignOut();
        Assert.True(model.ResolveInvoiceLeave(InvoiceLeaveChoice.Discard)); Assert.Null(model.CurrentUsername);
    }

    [Fact]
    public void SaveFailure_KeepsDraftAndDestinationPending()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.StartDocument("Invoice"); model.InvoiceOptions[2].Value = "Cannot save without items";
        model.NavigateCommand.Execute("Dashboard");
        Assert.False(model.ResolveInvoiceLeave(InvoiceLeaveChoice.Save));
        Assert.Equal("New Invoice", model.Title); Assert.True(model.HasPendingInvoiceNavigation);
        Assert.Contains("at least one item", model.Status); Assert.True(model.HasUnsavedInvoiceChanges);
        model.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing);
    }

    [Fact]
    public void SuccessfulSave_NavigatesAndExistingInvoiceLoadsClean()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.StartDocument("Invoice");
        model.Lines.Add(new() { Name = "Service", ProductType = "Service", Quantity = 1, Price = 100 });
        model.NavigateCommand.Execute("Dashboard");
        Assert.True(model.ResolveInvoiceLeave(InvoiceLeaveChoice.Save), model.Status);
        Assert.Equal("Dashboard", model.Title); Assert.False(model.HasUnsavedInvoiceChanges);
        Assert.NotNull(model.LastSavedDocument);
        Assert.True(model.LoadDocumentForEditing(model.LastSavedDocument!), model.Status);
        Assert.False(model.HasUnsavedInvoiceChanges);
        model.NavigateCommand.Execute("Invoices"); Assert.Equal("Invoices", model.Title);
    }

    [Theory]
    [InlineData("customer")] [InlineData("business")] [InlineData("phone")]
    [InlineData("email")] [InlineData("gst")] [InlineData("address")]
    [InlineData("date")] [InlineData("due")] [InlineData("time")]
    [InlineData("discount")] [InlineData("tax")] [InlineData("notes")]
    [InlineData("charges")] [InlineData("number")] [InlineData("hide")]
    [InlineData("interstate")] [InlineData("items")]
    [InlineData("bank")] [InlineData("currency")] [InlineData("type")]
    [InlineData("upi")]
    public void MeaningfulChanges_AreInSnapshot(string change)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(); model.StartDocument("Invoice");
        switch (change)
        {
            case "customer": model.InvoiceCustomer[0].Value = "Changed"; break;
            case "business": model.InvoiceCustomer[1].Value = "Changed"; break;
            case "phone": model.InvoiceCustomer[2].Value = "555"; break;
            case "email": model.InvoiceCustomer[3].Value = "mail@example.com"; break;
            case "gst": model.InvoiceCustomer[4].Value = "Tax"; break;
            case "address": model.InvoiceCustomer[5].Value = "Address"; break;
            case "date": model.InvoiceDetails[1].Value = "2027-01-01"; break;
            case "due": model.InvoiceDetails[2].Value = "2027-02-01"; break;
            case "time": model.OrderTime.Value = "01:02"; break;
            case "discount": model.InvoiceOptions[1].Value = "12"; break;
            case "tax": model.InvoiceOptions[4].Value = "22"; break;
            case "notes": model.InvoiceOptions[2].Value = "Notes"; break;
            case "charges": model.AdditionalCosts.Add(InvoiceOptionsViewModel.CreateCost("Freight", -10)); break;
            case "number": model.InvoiceDetails[4].Value = "Custom"; break;
            case "hide": model.HideInvoiceNumber.IsChecked = !model.HideInvoiceNumber.IsChecked; break;
            case "interstate": model.InterState.IsChecked = !model.InterState.IsChecked; break;
            case "items": model.Lines.Add(new() { Name = "Draft", Quantity = 2, Price = 3 }); break;
            case "bank": model.SelectedInvoiceBank = new(new("Selected", "Bank", "Branch", "123", "IFSC", "")); break;
            case "currency": model.InvoiceSetting("Currency").Value = "USD"; break;
            case "type": model.InvoiceDetails[0].Value = "Quotation"; break;
            case "upi": model.UpiAccounts.Add([new("UPI ID", "changed@upi")]); break;
        }
        Assert.True(model.HasUnsavedInvoiceChanges);
        model.MarkInvoiceFormClean(); Assert.False(model.HasUnsavedInvoiceChanges);
    }
}

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class InvoiceNavigationGuardUiTests(HeadlessFixture headless)
{
    [Fact]
    public Task CtrlQ_AndSidebarRenderExistingAxamlConfirmation() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(false);
        using (var db = fixture.CreateDbContext()) { db.Users.Single(user => user.Username == "admin").PasswordChanged = true; db.SaveChanges(); }
        Assert.True(model.SignIn("admin", "admin")); model.StartDocument("Invoice");
        var window = new MainWindow { DataContext = model, Width = 1366, Height = 768 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            model.InvoiceOptions[2].Value = "Draft note";
            window.KeyBindings.Single(binding => binding.Gesture.Key == Key.Q).Command!.Execute(null);
            Assert.True(model.HasPendingInvoiceNavigation); Assert.Equal("Draft note", model.InvoiceOptions[2].Value);
            Assert.IsType<OverlayDialogViewModel>(model.CurrentOverlay);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var keep = window.GetVisualDescendants().OfType<Button>().Single(button => button.Content?.ToString() == UiLocalization.TranslateKey("createInvoiceKeepEditingButton"));
            keep.Command!.Execute(null); Assert.Null(model.CurrentOverlay); Assert.Equal("Draft note", model.InvoiceOptions[2].Value);
            model.NavigateCommand.Execute("Dashboard"); Assert.True(model.HasPendingInvoiceNavigation);
            var dialog = Assert.IsType<OverlayDialogViewModel>(model.CurrentOverlay);
            var actions = Assert.IsType<DialogActions>(dialog.Footer);
            actions.Items.Single(action => action.Label == UiLocalization.TranslateKey("actionSave")).Command!.Execute(null);
            Assert.Equal("New Invoice", model.Title); Assert.Same(dialog, model.CurrentOverlay);
            actions.Items.Single(action => action.Label == UiLocalization.TranslateKey("actionDiscard")).Command!.Execute(null);
            Assert.Equal("Dashboard", model.Title); Assert.Null(model.CurrentOverlay);
        }
        finally { model.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing); model.MarkInvoiceFormClean(); window.Close(); }
    });
}
