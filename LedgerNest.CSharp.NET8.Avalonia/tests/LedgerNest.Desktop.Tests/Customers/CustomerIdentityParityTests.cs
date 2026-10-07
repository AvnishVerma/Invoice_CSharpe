using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Customers;

public sealed class CustomerIdentityParityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Identity_ExcludesAddressNormalizesCaseAndRejectsAmbiguousMatches()
    {
        var form = new InvoiceCustomerSnapshot(" Alice ", "Business", "123", "mail@example.com", "GST", "New address");
        var customer = new Customer { Id = 2, Name = "alice", BusinessName = "business", Phone = " 123 ", Email = "MAIL@example.com", GstNumber = "gst", Address = "Old address" };
        Assert.True(CustomerIdentityRules.Matches(form, CustomerIdentityRules.Snapshot(customer)));
        Assert.Equal(2, CustomerIdentityRules.FindUniqueId(form, [customer]));
        Assert.Null(CustomerIdentityRules.FindUniqueId(form, [customer, new Customer { Id = 3, Name = "alice", BusinessName = "Business", Phone = "123", Email = "mail@example.com", GstNumber = "GST" }]));
        Assert.False(CustomerIdentityRules.Matches(form with { Phone = "999" }, CustomerIdentityRules.Snapshot(customer)));
    }
    private static void Seed(TestDatabaseFixture fixture)
    {
        using var db = fixture.CreateDbContext();
        db.Customers.AddRange(new Customer { Name = "Same name", Phone = "111", Address = "Master A", CustomerCode = "CUS-A" }, new Customer { Name = "Same name", Phone = "222", Address = "Master B", CustomerCode = "CUS-B" }); db.SaveChanges();
    }
    private static MainWindowViewModel Editor(TestDatabaseFixture fixture)
    {
        var model = fixture.CreateModel(); model.InvoiceOptions[3].Value = "No Tax";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100 }); return model;
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void ExplicitSelection_DisambiguatesNameAndKeepsAddressSnapshotIndependent()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = Editor(fixture);
        var intended = model.Customers.Single(customer => customer["Phone"] == "222");
        Assert.True(model.SelectInvoiceCustomer(intended)); Assert.True(model.CustomerIdentityLocked);
        Assert.True(model.InvoiceCustomer[0].IsReadOnly); Assert.False(model.InvoiceCustomer[5].IsReadOnly);
        model.InvoiceCustomer[5].Value = "Invoice shipping address";
        Assert.True(model.SaveInvoice(), model.Status);
        using var db = fixture.CreateDbContext(); var invoice = db.Invoices.Single();
        Assert.Equal(intended.SourceId, invoice.CustomerId); Assert.Equal("Invoice shipping address", invoice.Snapshot!.Customer.Address);
        Assert.Equal("Master B", db.Customers.Single(customer => customer.Id == intended.SourceId).Address);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void UnsavedIdentityChange_DetachesInvoiceWithoutCreatingOrUpdatingCustomer()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = Editor(fixture);
        Assert.True(model.SelectInvoiceCustomer(model.Customers.Single(customer => customer["Phone"] == "111")));
        model.CustomerFieldsUnlocked = true; Assert.False(model.InvoiceCustomer[0].IsReadOnly);
        model.InvoiceCustomer[2].Value = "333";
        Assert.True(model.SaveInvoice(), model.Status);
        using var db = fixture.CreateDbContext(); Assert.Null(db.Invoices.Single().CustomerId); Assert.Equal("333", db.Invoices.Single().Snapshot!.Customer.Phone);
        Assert.Equal(2, db.Customers.Count()); Assert.DoesNotContain(db.Customers, customer => customer.Phone == "333");
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void ManualIdentity_MatchesOnlyUniqueCompleteIdentity()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = Editor(fixture);
        model.InvoiceCustomer[0].Value = " same NAME "; model.InvoiceCustomer[2].Value = "222";
        Assert.True(model.SaveInvoice());
        using var db = fixture.CreateDbContext(); Assert.Equal(db.Customers.Single(customer => customer.Phone == "222").Id, db.Invoices.Single().CustomerId);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void ManualIdentity_UsesUnicodeNameMatchingWithoutDatabaseLowercaseAssumptions()
    {
        using var fixture = new TestDatabaseFixture();
        using (var db = fixture.CreateDbContext()) { db.Customers.Add(new Customer { Name = "Émilie", Phone = "333" }); db.SaveChanges(); }
        var model = Editor(fixture); model.InvoiceCustomer[0].Value = " éMILIE "; model.InvoiceCustomer[2].Value = "333";
        Assert.True(model.SaveInvoice(), model.Status);
        using var result = fixture.CreateDbContext(); Assert.Equal(result.Customers.Single().Id, result.Invoices.Single().CustomerId);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void SelectedMasterSave_UpdatesExistingAndRejectsPhoneCollision()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = fixture.CreateModel();
        var intended = model.Customers.Single(customer => customer["Phone"] == "111"); Assert.True(model.SelectInvoiceCustomer(intended));
        model.CustomerFieldsUnlocked = true; model.InvoiceCustomer[2].Value = "333";
        Assert.True(model.SaveInvoiceCustomer(), model.Status); Assert.Equal(intended.SourceId, model.SelectedInvoiceCustomerId); Assert.True(model.CustomerIdentityLocked);
        model.CustomerFieldsUnlocked = true; model.InvoiceCustomer[2].Value = "222";
        Assert.False(model.SaveInvoiceCustomer()); Assert.Contains("another customer", model.Status);
        using var db = fixture.CreateDbContext(); Assert.Equal(2, db.Customers.Count()); Assert.Equal("333", db.Customers.Single(customer => customer.Id == intended.SourceId).Phone);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void SaveMaster_ResolvesExistingIdentityWithoutCreatingDuplicate()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = fixture.CreateModel();
        model.InvoiceCustomer[0].Value = "Same name"; model.InvoiceCustomer[2].Value = "111"; model.InvoiceCustomer[5].Value = "Edited address";
        Assert.True(model.SaveInvoiceCustomer(), model.Status);
        using var db = fixture.CreateDbContext(); Assert.Equal(2, db.Customers.Count()); Assert.Equal("CUS-A", db.Customers.Single(customer => customer.Phone == "111").CustomerCode);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void StaleSelection_RequiresRefreshAndRefreshRelocksFields()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = fixture.CreateModel();
        var intended = model.Customers.Single(customer => customer["Phone"] == "111"); Assert.True(model.SelectInvoiceCustomer(intended));
        model.CustomerFieldsUnlocked = true;
        using (var db = fixture.CreateDbContext()) { db.Customers.Single(customer => customer.Id == intended.SourceId).Name = "Changed"; db.SaveChanges(); }
        Assert.False(model.SaveInvoiceCustomer()); Assert.Contains("changed", model.Status);
        Assert.True(model.RefreshInvoiceCustomer()); Assert.Equal("Changed", model.InvoiceCustomer[0].Value); Assert.True(model.CustomerIdentityLocked); Assert.False(model.CustomerFieldsUnlocked);
        model.StartDocument("Invoice"); Assert.Null(model.SelectedInvoiceCustomerId); Assert.All(model.InvoiceCustomer, field => Assert.False(field.IsReadOnly));
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void DeletedSelection_RejectsMasterSaveAndDoesNotRelinkToNamesake()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = Editor(fixture);
        var intended = model.Customers.Single(customer => customer["Phone"] == "111"); Assert.True(model.SelectInvoiceCustomer(intended));
        using (var db = fixture.CreateDbContext()) { db.Customers.Remove(db.Customers.Single(customer => customer.Id == intended.SourceId)); db.SaveChanges(); }
        Assert.False(model.SaveInvoiceCustomer()); Assert.True(model.SaveInvoice());
        using var intact = fixture.CreateDbContext(); Assert.Single(intact.Customers); Assert.Null(intact.Invoices.Single().CustomerId);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public async Task UserSwitch_ClearsSelectionAndCannotUpdateMaster()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var model = fixture.CreateModel();
        Assert.True(model.SelectInvoiceCustomer(model.Customers.First()));
        await TestData.CreateRole(model, "InvoiceViewer"); TestData.CreateUser(model, "viewer", "InvoiceViewer");
        TestData.SetPermissions(fixture, "InvoiceViewer", "Invoice", "View");
        model.SignOut(); Assert.Null(model.SelectedInvoiceCustomerId); Assert.All(model.InvoiceCustomer, field => Assert.Empty(field.Value));
        Assert.True(model.SignIn("viewer", TestData.Password)); Assert.True(model.SelectInvoiceCustomer(model.Customers.First()));
        Assert.False(model.CanSaveInvoiceCustomer); model.CustomerFieldsUnlocked = true; model.InvoiceCustomer[0].Value = "Denied";
        Assert.False(model.SaveInvoiceCustomer()); using var db = fixture.CreateDbContext(); Assert.DoesNotContain(db.Customers, customer => customer.Name == "Denied");
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void DatabaseFailure_CommandsFailSafelyAndMasterRemainsIntact()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var factory = new SwitchableFactory(fixture);
        var model = new MainWindowViewModel(factory, fixture.DatabasePath, new TestLicenseService()); Assert.True(model.SignIn("admin", "admin"));
        var selected = model.Customers.First(); Assert.True(model.SelectInvoiceCustomer(selected));
        factory.Fail = true;
        Assert.False(model.CanSaveInvoiceCustomer); Assert.False(model.SaveInvoiceCustomer()); Assert.Contains("Unable to save", model.Status);
        Assert.False(model.RefreshInvoiceCustomer()); Assert.Contains("Unable to load", model.Status); Assert.Null(model.SelectedInvoiceCustomerId);
        factory.Fail = false;
        using var intact = fixture.CreateDbContext(); Assert.Equal(2, intact.Customers.Count()); Assert.Contains(intact.Customers, customer => customer.Id == selected.SourceId && customer.Phone == selected["Phone"]);
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void MasterSave_ReloadFailureDoesNotMisreportOrUndoCommittedChange()
    {
        using var fixture = new TestDatabaseFixture(); Seed(fixture); var factory = new SwitchableFactory(fixture);
        var model = new MainWindowViewModel(factory, fixture.DatabasePath, new TestLicenseService()); Assert.True(model.SignIn("admin", "admin"));
        var selected = model.Customers.First(); Assert.True(model.SelectInvoiceCustomer(selected));
        model.CustomerFieldsUnlocked = true; model.InvoiceCustomer[0].Value = "Committed";
        factory.FailWhen = () => { using var probe = fixture.CreateDbContext(); return probe.Customers.AsNoTracking().Any(customer => customer.Name == "Committed"); };
        Assert.True(model.SaveInvoiceCustomer(), model.Status); Assert.Contains("Customer saved", model.Status); Assert.Contains("could not reload", model.Status);
        Assert.Null(model.SelectedInvoiceCustomerId); factory.FailWhen = null;
        using var db = fixture.CreateDbContext(); Assert.Equal("Committed", db.Customers.Single(customer => customer.Id == selected.SourceId).Name); Assert.Equal(2, db.Customers.Count());
    }
    private sealed class SwitchableFactory(IDbContextFactory<LedgerNestDbContext> inner) : IDbContextFactory<LedgerNestDbContext>
    {
        public bool Fail { get; set; }
        public Func<bool>? FailWhen { get; set; }
        public LedgerNestDbContext CreateDbContext() => Fail || FailWhen?.Invoke() == true ? throw new IOException("Injected database failure") : inner.CreateDbContext();
    }
}
