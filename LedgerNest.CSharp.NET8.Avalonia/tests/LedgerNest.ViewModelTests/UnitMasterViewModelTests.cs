using LedgerNest.Desktop;
using LedgerNest.Infrastructure;
using LedgerNest.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.ViewModelTests;

public sealed class UnitMasterViewModelTests
{
    [Fact]
    public async Task AdminCanCreateEditAndDeactivateUnit()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext()) db.EnsureCurrentSchema();
            var model = new UnitMasterViewModel(fixture.Factory, () => "admin", () => "Admin");
            model.NewCommand.Execute(null);
            Assert.True(model.IsEditing);
            model.Code = "box"; model.UnitName = "Box"; model.Description = "Ten pieces";
            await model.SaveCommand.ExecuteAsync(null);
            Assert.Single(model.Units);
            Assert.Equal("BOX", model.Units[0].Code);
            model.SelectedUnit = model.Units[0]; model.EditSelectedCommand.Execute(null); model.IsActive = false;
            await model.SaveCommand.ExecuteAsync(null);
            Assert.False(model.Units[0].IsActive);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task UserWithoutCreatePermissionCannotSaveUnit()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext()) db.EnsureCurrentSchema();
            var model = new UnitMasterViewModel(fixture.Factory, () => "sales", () => "User") { Code = "PCS", UnitName = "Piece" };
            await model.SaveCommand.ExecuteAsync(null);
            Assert.Empty(model.Units);
            Assert.Contains("cannot create units", model.Error);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task ProductAndInvoiceEditorsReuseActiveMasterUnits()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                db.Units.AddRange(new UnitOfMeasure { Code = "BOX", Name = "Box", IsActive = true }, new UnitOfMeasure { Code = "OLD", Name = "Old", IsActive = false });
                await db.SaveChangesAsync();
            }
            var model = new MainWindowViewModel(fixture.Factory, fixture.Path);
            var productFields = model.ProductEditorFields();
            var options = productFields.Single(field => field.Label == "Unit").Options;
            Assert.Contains("BOX", options);
            Assert.DoesNotContain("OLD", options);
            var product = new UiRecord { Values = new() { ["Name"] = "Fixture", ["Unit"] = "BOX", ["Sale Price"] = "10" } };
            var dialog = new ProductItemDialogViewModel(product, _ => { }, () => { }, unitOptions: model.ActiveUnitCodes("BOX"));
            Assert.Contains("BOX", dialog.Unit.Options);
            Assert.DoesNotContain("OLD", dialog.Unit.Options);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public void SelectingSellingUnitAppliesEffectivePriceAndPreservesConversion()
    {
        InvoiceLineViewModel? added = null;
        var product = new UiRecord { SourceId = 12, Values = new() { ["Name"] = "Paper", ["Unit"] = "PCS", ["Sale Price"] = "10" } };
        var dialog = new ProductItemDialogViewModel(product, line => added = line, () => { }, sellingUnitOptions:
        [
            new SellingUnitChoice(4, "PCS", 1, 10),
            new SellingUnitChoice(5, "BOX", 20, 190)
        ]);

        dialog.Unit.Value = "BOX";
        Assert.Equal(190m, dialog.Price.Number);
        dialog.Quantity.Value = "2";
        dialog.AddCommand.Execute(null);

        Assert.NotNull(added);
        Assert.Equal(5, added.SellingUnitId);
        Assert.Equal(20m, added.UnitConversionFactor);
        Assert.Equal(40m, added.BaseQuantity);
        Assert.Equal(190m, added.Price);
    }

    [Fact]
    public async Task InvoiceSellingUnitUsesPriceMasterThenFallsBackToProductPrice()
    {
        var fixture = Factory();
        try
        {
            int sellingUnitId;
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                var unit = new UnitOfMeasure { Code = "BOX", Name = "Box", IsActive = true };
                var product = new Product { ProductCode = "PRD-PRICE", Name = "Priced product", Type = "Product", Unit = "BOX", SalePrice = 125 };
                db.AddRange(unit, product);
                await db.SaveChangesAsync();
                var sellingUnit = new ProductSellingUnit { ProductId = product.Id, UnitId = unit.Id, ConversionFactor = 5, SellingPrice = 0, IsDefault = true, IsActive = true };
                db.ProductSellingUnits.Add(sellingUnit);
                await db.SaveChangesAsync();
                sellingUnitId = sellingUnit.Id;
            }

            var model = new MainWindowViewModel(fixture.Factory, fixture.Path);
            var productRecord = model.Products.Single(item => item["Product ID"] == "PRD-PRICE");
            Assert.Equal(125m, model.SellingUnitChoices(productRecord).Single(item => item.SellingUnitId == sellingUnitId).Price);

            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.ProductPrices.Add(new ProductPrice
                {
                    ProductId = productRecord.SourceId,
                    SellingUnitId = sellingUnitId,
                    SellingPrice = 110,
                    EffectiveDate = DateTime.UtcNow.AddMinutes(-1),
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            Assert.Equal(110m, model.SellingUnitChoices(productRecord).Single(item => item.SellingUnitId == sellingUnitId).Price);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task SignedInAdministratorCanSavePriceMasterAndInvoiceUsesIt()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                db.Units.Add(new UnitOfMeasure { Code = "PACK", Name = "Pack", IsActive = true });
                db.Products.Add(new Product { ProductCode = "PRD-MASTER", Name = "Master priced", Type = "Product", Unit = "PACK", SalePrice = 80 });
                await db.SaveChangesAsync();
            }

            var model = new MainWindowViewModel(fixture.Factory, fixture.Path);
            Assert.False(model.ProductPriceMaster.CanManage);
            Assert.True(model.SignIn("admin", "admin"));
            Assert.True(model.ProductPriceMaster.CanManage);

            var prices = model.ProductPriceMaster;
            prices.SelectedProduct = prices.Products.Single(item => item.Name == "Master priced");
            prices.SelectedUnit = prices.Units.Single(item => item.Code == "PACK");
            prices.ConversionFactor = 4;
            prices.UnitSellingPrice = 0;
            prices.IsDefault = true;
            await prices.SaveSellingUnitCommand.ExecuteAsync(null);
            prices.SelectedSellingUnit = prices.SellingUnits.Single();
            prices.EffectiveSellingPrice = 72;
            prices.EffectiveDate = DateTimeOffset.Now.AddMinutes(-1);
            await prices.SavePriceCommand.ExecuteAsync(null);

            Assert.Equal("Effective price saved.", prices.Status);
            var product = model.Products.Single(item => item["Product ID"] == "PRD-MASTER");
            Assert.Equal(72m, model.SellingUnitChoices(product).Single(item => item.Code == "PACK").Price);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task InventoryScreenPostsAdjustmentAndRefreshesHistory()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                db.Products.Add(new Product { ProductCode = "PRD001", Name = "Tracked", Type = "Product", Unit = "PCS", StockQuantity = 10 });
                db.Users.Add(new AppUser { Username = "admin", Role = "Admin", PasswordHash = "hash", Salt = "salt" });
                await db.SaveChangesAsync();
            }
            var model = new InventoryViewModel(fixture.Factory, () => "admin", () => "Admin");
            Assert.Single(model.Products);
            model.Direction = "Stock Out";
            model.MovementType = "Damage";
            model.Quantity = 2;
            model.Reference = "DAMAGE-1";
            await model.PostCommand.ExecuteAsync(null);

            Assert.Equal(8m, model.Products.Single().Stock);
            Assert.Single(model.Movements);
            Assert.Equal(-2m, model.Movements.Single().Change);
            Assert.Equal("DAMAGE-1", model.Movements.Single().Reference);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task QuotationCancellationViewModelRequiresReasonAndUpdatesRecord()
    {
        var fixture = Factory();
        try
        {
            int quotationId;
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                var quotation = new Invoice { InvoiceNumber = "QUO-7", Type = "Quotation", Status = "Open" };
                db.Invoices.Add(quotation); await db.SaveChangesAsync(); quotationId = quotation.Id;
            }
            var main = new MainWindowViewModel(fixture.Factory, fixture.Path);
            Assert.True(main.SignIn("admin", "admin"));
            var record = new UiRecord { SourceId = quotationId, Values = new() { ["Name"] = "QUO-7", ["Type"] = "Quotation", ["Status"] = "Open", ["Customer"] = "Test" } };
            var closed = false;
            var model = new QuotationCancellationViewModel(main, record, () => { }, () => closed = true);
            await model.CancelQuotationCommand.ExecuteAsync(null);
            Assert.Equal("Cancellation reason is required.", model.Error);
            model.Reason = "Duplicate request";
            await model.CancelQuotationCommand.ExecuteAsync(null);
            Assert.True(closed);
            Assert.Equal("Cancelled", record["Status"]);
            Assert.Equal("Duplicate request", record["Cancellation Reason"]);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task RefundViewModelCreatesPartialRefundAndUpdatesInventory()
    {
        var fixture = Factory();
        try
        {
            int invoiceId;
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                var product = new Product { ProductCode = "PRD-R", Name = "Return", Type = "Product", StockQuantity = 5 };
                var invoice = new Invoice { InvoiceNumber = "INV-R", Type = "Invoice", Status = "Paid" };
                db.AddRange(product, invoice); await db.SaveChangesAsync();
                db.InvoiceItems.Add(new InvoiceItem { InvoiceId = invoice.Id, ProductId = product.Id, Description = "Return", Quantity = 3, UnitPrice = 50, UnitConversionFactor = 2, BaseQuantity = 6 });
                await db.SaveChangesAsync(); invoiceId = invoice.Id;
            }
            var record = new UiRecord { SourceId = invoiceId, Values = new() { ["Name"] = "INV-R", ["Customer"] = "Buyer", ["Type"] = "Invoice" } };
            var closed = false;
            var model = new InvoiceRefundViewModel(fixture.Factory, record, () => "admin", () => { }, () => closed = true);
            model.Lines.Single().RefundQuantity = 1;
            model.Reason = "Returned";
            await model.SubmitCommand.ExecuteAsync(null);
            Assert.True(closed);
            Assert.Equal(50m, model.RefundTotal);
            await using var verify = fixture.Factory.CreateDbContext();
            Assert.Equal(7m, (await verify.Products.SingleAsync()).StockQuantity);
            Assert.Single(await verify.InvoiceRefunds.ToListAsync());
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public void ProductSearchUsesBarcodeProductCodeAndMultipleTerms()
    {
        var model = new MainWindowViewModel();
        model.Products.Clear();
        model.Products.Add(new UiRecord { SourceId = 1, Values = new() { ["Name"] = "Blue Ball Pen", ["Product ID"] = "PRD007", ["SKU Code"] = "PEN-B", ["Barcode"] = "890123", ["HSN/SAC"] = "9608", ["Description"] = "Blue ink stationery" } });
        model.Products.Add(new UiRecord { SourceId = 2, Values = new() { ["Name"] = "Laptop", ["Product ID"] = "PRD008", ["Barcode"] = "777" } });

        Assert.Equal("Blue Ball Pen", model.SearchProducts("890123").Single().Name);
        Assert.Equal("Blue Ball Pen", model.SearchProducts("blue stationery").Single().Name);
        Assert.Equal("Blue Ball Pen", model.ExactProductCodeMatches("prd007").Single().Name);
        Assert.Empty(model.SearchProducts("blue laptop"));
    }

    [Fact]
    public async Task BarcodeReportFiltersAndExpandsSelectedLabels()
    {
        BarcodeLabelData[]? exported = null;
        UiRecord[] products =
        [
            new() { Values = new() { ["Name"] = "Ball Pen", ["Type"] = "Product", ["Product ID"] = "PRD1", ["Barcode"] = "8901", ["Sale Price"] = "12" } },
            new() { Values = new() { ["Name"] = "Service", ["Type"] = "Service", ["Product ID"] = "SRV1" } }
        ];
        var model = new BarcodeReportViewModel(products, labels => { exported = labels; return Task.CompletedTask; });
        Assert.Single(model.VisibleRows);
        model.SearchText = "8901";
        model.VisibleRows.Single().IsSelected = true;
        model.VisibleRows.Single().Quantity = 3;
        await model.ExportCommand.ExecuteAsync(null);
        Assert.Equal(3, exported!.Length);
        Assert.All(exported, label => Assert.Equal("8901", label.Code));
        var pdf = BarcodeLabelPdf.Create(exported, "INR");
        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task AdministratorCanPersistRolePermissionMatrix()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                db.Users.Add(new AppUser { Username = "sales-user", Role = "User", PasswordHash = "hash", Salt = "salt" });
                await db.SaveChangesAsync();
            }
            var model = new PermissionManagementViewModel(fixture.Factory, () => "Admin");
            model.SelectedRole = null!;
            Assert.NotEmpty(model.ResourcePermissions);
            model.NewRoleName = "Sales";
            await model.CreateRoleCommand.ExecuteAsync(null);
            model.SelectedRole = "Sales";
            model.Permissions.Single(item => item.Resource == "Invoice" && item.Action == "View").IsAllowed = true;
            model.Permissions.Single(item => item.Resource == "Invoice" && item.Action == "Update").IsAllowed = true;
            var invoiceRow = model.ResourcePermissions.Single(item => item.Resource == "Invoice");
            invoiceRow.View.IsAllowed = false;
            Assert.False(invoiceRow.Update.IsAllowed);
            Assert.False(invoiceRow.Update.CanToggle);
            invoiceRow.View.IsAllowed = true;
            invoiceRow.Update.IsAllowed = true;
            await model.SaveCommand.ExecuteAsync(null);
            Assert.Equal(model.ResourcePermissions.Count, model.ResourcePermissions.Select(item => item.Resource).Distinct().Count());
            Assert.True(model.ResourcePermissions.Single(item => item.Resource == "Invoice").Update.IsAllowed);
            Assert.True(await new AuthorizationService(fixture.Factory).IsAllowedAsync("Sales", "Invoice", "Update"));
            Assert.False(await new AuthorizationService(fixture.Factory).IsAllowedAsync("Sales", "Inventory", "Update"));
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task AssignedRoleIsAppliedAfterUserSignsIn()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                var salt = PasswordCredentials.CreateSalt();
                var adminSalt = PasswordCredentials.CreateSalt();
                db.Users.AddRange(
                    new AppUser { Username = "admin", Role = "Admin", PasswordHash = PasswordCredentials.Hash("admin", adminSalt), Salt = adminSalt, PasswordChanged = true },
                    new AppUser { Username = "operator", Role = "User", PasswordHash = PasswordCredentials.Hash("operator-pass", salt), Salt = salt, PasswordChanged = true });
                await db.SaveChangesAsync();
            }
            var main = new MainWindowViewModel(fixture.Factory, fixture.Path);
            Assert.True(main.SignIn("admin", "admin"));
            Assert.True(main.PermissionManagement.CanManage);
            var permissions = main.PermissionManagement;
            permissions.NewRoleName = "Sales";
            await permissions.CreateRoleCommand.ExecuteAsync(null);
            permissions.SelectedRole = "Sales";
            permissions.Permissions.Single(item => item.Resource == "Invoice" && item.Action == "View").IsAllowed = true;
            permissions.Permissions.Single(item => item.Resource == "Invoice" && item.Action == "Add").IsAllowed = true;
            await permissions.SaveCommand.ExecuteAsync(null);
            var userFields = FormCatalog.User(permissions.Roles);
            userFields[0].Value = "operator";
            userFields[1].Value = "operator-pass";
            userFields[2].Value = "Sales";
            Assert.True(main.SaveRecord("User", userFields, main.Users.Single(item => item.Name == "operator")));

            main.SignOut();
            Assert.False(main.PermissionManagement.CanManage);
            Assert.Empty(main.VisibleRoutes);
            Assert.True(main.SignIn("operator", "operator-pass"));
            Assert.Equal("Sales", main.CurrentRole);
            Assert.Contains("Invoices", main.VisibleRoutes);
            Assert.DoesNotContain("Inventory", main.VisibleRoutes);
            Assert.True(main.HasPermission("Invoice", "Add"));
            Assert.False(main.HasPermission("Inventory", "Update"));
            main.SignOut();
            Assert.True(main.SignIn("admin", "admin"));
            Assert.Equal(MainWindowViewModel.Routes.Length, main.VisibleRoutes.Count());
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task MultipleRolesCombinePermissionsAndDirectNavigationIsDenied()
    {
        var fixture = Factory();
        try
        {
            int userId;
            await using (var db = fixture.Factory.CreateDbContext())
            {
                db.EnsureCurrentSchema();
                var salt = PasswordCredentials.CreateSalt();
                var adminSalt = PasswordCredentials.CreateSalt();
                var user = new AppUser { Username = "multi-role", Role = "Viewer", PasswordHash = PasswordCredentials.Hash("password", salt), Salt = salt, PasswordChanged = true };
                db.Users.AddRange(new AppUser { Username = "admin", Role = "Admin", PasswordHash = PasswordCredentials.Hash("admin", adminSalt), Salt = adminSalt }, user);
                db.Roles.AddRange(new AppRole { Name = "Admin", IsSystem = true }, new AppRole { Name = "Viewer" }, new AppRole { Name = "Creator" });
                await db.SaveChangesAsync(); userId = user.Id;
                var roles = await db.Roles.ToDictionaryAsync(item => item.Name);
                db.UserRoles.AddRange(new AppUserRole { UserId = userId, RoleId = roles["Viewer"].Id }, new AppUserRole { UserId = userId, RoleId = roles["Creator"].Id });
                db.RolePermissions.AddRange(
                    new RolePermission { Role = "Viewer", Resource = "Product", Action = "View", IsAllowed = true },
                    new RolePermission { Role = "Creator", Resource = "Product", Action = "Add", IsAllowed = true });
                await db.SaveChangesAsync();
            }

            var main = new MainWindowViewModel(fixture.Factory, fixture.Path);
            Assert.True(main.SignIn("multi-role", "password"));
            Assert.Contains("Products", main.VisibleRoutes);
            Assert.DoesNotContain("Customers", main.VisibleRoutes);
            main.NavigateCommand.Execute("Customers");
            Assert.NotEqual("Customers", main.Title);
            Assert.Contains("Access denied", main.Status);
            Assert.True(main.HasPermission("Product", "View"));
            Assert.True(main.HasPermission("Product", "Add"));
            Assert.False(main.HasPermission("Product", "Update"));
            Assert.False(main.HasPermission("Product", "Delete"));
            Assert.False(main.DeleteRecord("Product", new UiRecord()));
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task AdminRoleAlwaysHasEveryCatalogPermission()
    {
        var fixture = Factory();
        try
        {
            await using (var db = fixture.Factory.CreateDbContext()) db.EnsureCurrentSchema();
            var main = new MainWindowViewModel(fixture.Factory, fixture.Path);
            Assert.True(main.SignIn("admin", "admin"));
            Assert.All(RbacCatalog.Permissions, permission => Assert.True(main.HasPermission(permission.Resource, permission.Action)));
            Assert.Equal(MainWindowViewModel.Routes.Length, main.VisibleRoutes.Count());
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task SavedUnpaidInvoiceCanBeVoidedOnlyOnce()
    {
        var fixture = Factory();
        try
        {
            await SeedInvoice(fixture.Factory, paidAmount: 0);
            var model = new MainWindowViewModel(fixture.Factory, fixture.Path, new ActiveTestLicense());
            Assert.True(model.SignIn("admin", "admin"));
            var record = model.Invoices.Single(item => item.Name == "INV-VOID-1");
            Assert.True(model.LoadDocumentForEditing(record));
            Assert.True(model.CanVoidCurrentInvoice);
            Assert.True(model.VoidCurrentInvoice(), model.Status);
            Assert.Equal("Voided", record["Status"]);
            Assert.False(model.CanVoidCurrentInvoice);
            Assert.False(model.VoidCurrentInvoice());
            Assert.Contains("already voided", model.Status, StringComparison.OrdinalIgnoreCase);

            await using var db = fixture.Factory.CreateDbContext();
            var invoice = await db.Invoices.SingleAsync(item => item.InvoiceNumber == "INV-VOID-1");
            Assert.Equal("Voided", invoice.Status);
            Assert.Equal("admin", invoice.CancelledBy);
            Assert.NotNull(invoice.CancelledAt);
        }
        finally { Cleanup(fixture.Path); }
    }

    [Fact]
    public async Task InvoiceWithPaymentCannotBeVoided()
    {
        var fixture = Factory();
        try
        {
            await SeedInvoice(fixture.Factory, paidAmount: 25);
            var model = new MainWindowViewModel(fixture.Factory, fixture.Path, new ActiveTestLicense());
            Assert.True(model.SignIn("admin", "admin"));
            var record = model.Invoices.Single(item => item.Name == "INV-VOID-1");
            Assert.True(model.LoadDocumentForEditing(record), model.Status);
            Assert.False(model.CanVoidCurrentInvoice);
            Assert.False(model.VoidCurrentInvoice());
            Assert.Contains("payment", model.Status, StringComparison.OrdinalIgnoreCase);
            await using var db = fixture.Factory.CreateDbContext();
            Assert.NotEqual("Voided", (await db.Invoices.SingleAsync()).Status);
        }
        finally { Cleanup(fixture.Path); }
    }

    private static async Task SeedInvoice(IDbContextFactory<LedgerNestDbContext> factory, decimal paidAmount)
    {
        await using var db = factory.CreateDbContext();
        db.EnsureCurrentSchema();
        db.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-VOID-1",
            Type = "Invoice",
            Status = paidAmount > 0 ? "Partial" : "Unpaid",
            GrandTotal = 100,
            PaidAmount = paidAmount,
            Snapshot = new InvoiceSnapshot(1,
                new InvoiceCustomerSnapshot("Customer", "", "", "", "", ""),
                null, "Invoice", "", false, false, "INR", "Qty", "No Tax", 0,
                "None", 0, "", [])
        });
        await db.SaveChangesAsync();
    }

    private static (string Path, TestFactory Factory) Factory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-unit-vm-{Guid.NewGuid():N}.db");
        return (path, new TestFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options));
    }

    private static void Cleanup(string path) { SqliteConnection.ClearAllPools(); try { File.Delete(path); } catch (IOException) { } }
    private sealed class TestFactory(DbContextOptions<LedgerNestDbContext> options) : IDbContextFactory<LedgerNestDbContext>
    {
        public LedgerNestDbContext CreateDbContext() => new(options);
    }
    private sealed class ActiveTestLicense : ILicenseService
    {
        public string DeviceId => "test-device";
        public LicenseStatus GetStatus() => new(LicenseState.Active, "Test license", new LicenseClaims { Features = [LicenseFeatures.BusinessWrite] });
        public LicenseStatus Activate(string document) => GetStatus();
    }
}
