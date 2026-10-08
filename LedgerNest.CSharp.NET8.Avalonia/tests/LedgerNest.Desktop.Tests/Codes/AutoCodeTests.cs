using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Codes;

public sealed class AutoCodeTests
{
    [Theory]
    [InlineData("CUST", 1, 5, "CUST00001")]
    [InlineData("PROD", 2, 5, "PROD00002")]
    [InlineData("CUS-", 25, 5, "CUS-00025")]
    [InlineData("CUS-", 25, 0, "CUS-25")]
    [InlineData("CUS-", 1250, 3, "CUS-1250")]
    [InlineData("CUST", 25, 4, "CUST0025")]
    [InlineData("CUST-", 25, 6, "CUST-000025")]
    [InlineData("PROD", 100, 6, "PROD000100")]
    [InlineData("", 25, 5, "00025")]
    public void Formatting_DoesNotTruncate(string prefix, long number, int zeros, string expected) => Assert.Equal(expected, AutoCodeRules.Format(prefix, number, zeros));

    [Theory]
    [InlineData(0, 5)] [InlineData(-1, 5)] [InlineData(1, -1)] [InlineData(1, 19)] [InlineData(long.MaxValue, 5)]
    public void InvalidNumbers_AreRejected(long number, int zeros) => Assert.ThrowsAny<ArgumentException>(() => AutoCodeRules.Format("CUST", number, zeros));

    [Fact] public void PrefixValidationAndExhaustion_AreExplicit()
    {
        Assert.Throws<ArgumentException>(() => AutoCodeRules.Format(new string('X', 51), 1, 5));
        Assert.Throws<ArgumentException>(() => AutoCodeRules.Format("X\n", 1, 5));
        Assert.Throws<InvalidOperationException>(() => AutoCodeRules.NextCompatible(new("X"), ["X9223372036854775807"]));
    }

    [Theory] [InlineData("Customer", "CUST")] [InlineData("Product", "PROD")]
    [Trait("Category", "Integration")]
    public void GeneratedCodes_IncrementPersistAndDoNotChangeInvoiceNumbers(string entity, string prefix)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        var invoiceStart = model.InvoiceSetting("Starting Number").Value;
        Assert.True(model.SaveRecord(entity, Fields(entity, "One")), model.Status);
        Assert.True(model.SaveRecord(entity, Fields(entity, "Two")), model.Status);
        var records = entity == "Customer" ? model.Customers : model.Products;
        Assert.Equal(new[] { prefix + "00001", prefix + "00002" }, records.Select(item => item[entity + " ID"]));
        var restarted = fixture.CreateModel();
        Assert.Equal(prefix + "00002", (entity == "Customer" ? restarted.Customers : restarted.Products).Last()[entity + " ID"]);
        Assert.Equal(3, new AutoCodeGenerator(fixture).Load(entity).NextNumber);
        Assert.Equal(invoiceStart, restarted.InvoiceSetting("Starting Number").Value);
    }

    [Theory] [InlineData("Customer", "CUST")] [InlineData("Product", "PROD")]
    [Trait("Category", "Integration")]
    public void PrefixPaddingAndManualCodes_PreserveExistingRecords(string entity, string defaultPrefix)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(); var generator = new AutoCodeGenerator(fixture);
        Assert.True(model.SaveRecord(entity, Fields(entity, "Original")));
        generator.SaveSettings(new Dictionary<string, AutoCodeSettings> { [entity] = new("CL-", 25, 4) }, "admin");
        Assert.True(model.SaveRecord(entity, Fields(entity, "Changed")));
        generator.SaveSettings(new Dictionary<string, AutoCodeSettings> { [entity] = new("CL-", 1, 0) }, "admin");
        Assert.True(model.SaveRecord(entity, Fields(entity, "No padding")));
        Assert.True(model.SaveRecord(entity, Fields(entity, "Manual", "MANUAL-ONE")));
        var records = entity == "Customer" ? model.Customers : model.Products;
        Assert.Equal(new[] { defaultPrefix + "00001", "CL-0025", "CL-26", "MANUAL-ONE" }, records.Select(item => item[entity + " ID"]));
        Assert.False(model.SaveRecord(entity, Fields(entity, "Duplicate", "MANUAL-ONE"))); Assert.Contains("already in use", model.Status);
        var existing = records.First(); var edited = Fields(entity, "Edited", existing[entity + " ID"]);
        Assert.True(model.SaveRecord(entity, edited, existing)); Assert.Equal(defaultPrefix + "00001", records.First()[entity + " ID"]);
    }

    [Theory] [InlineData("Customer", "CUST")] [InlineData("Product", "PROD")]
    [Trait("Category", "Integration")]
    public void ExistingCompatibleCodes_InitializeNextWithoutRewritingLegacyCodes(string entity, string prefix)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext())
        {
            foreach (var code in new[] { "C001", prefix + "00001", prefix + "00004", prefix + "00017", prefix + "not-a-number" }) Persist(db, entity, code, code);
            db.SaveChanges();
        }
        Assert.Equal(18, new AutoCodeGenerator(fixture).Load(entity).NextNumber);
        Assert.True(model.SaveRecord(entity, Fields(entity, "New")));
        using var read = fixture.CreateDbContext();
        var codes = entity == "Customer" ? read.Customers.Select(item => item.CustomerCode).ToArray() : read.Products.Select(item => item.ProductCode).ToArray();
        Assert.Contains("C001", codes); Assert.Contains(prefix + "00017", codes); Assert.Contains(prefix + "00018", codes);
        Assert.Equal(6, codes.Length);
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void DisabledGeneration_RequiresManualCodeAndSettingsPersist(string entity)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(); var service = new AutoCodeGenerator(fixture);
        service.SaveSettings(new Dictionary<string, AutoCodeSettings> { [entity] = new("ITEM-", 100, 6, false) }, "admin");
        Assert.False(model.SaveRecord(entity, Fields(entity, "Blank"))); Assert.Contains("Enter a code", model.Status);
        Assert.True(model.SaveRecord(entity, Fields(entity, "Manual", "CUSTOM")));
        Assert.Equal(new AutoCodeSettings("ITEM-", 100, 6, false), new AutoCodeGenerator(fixture).Load(entity));
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void FailedInsert_RollsBackRecordAndSequence(string entity)
    {
        using var fixture = new TestDatabaseFixture(); fixture.CreateModel(); var service = new AutoCodeGenerator(fixture);
        Assert.Throws<InvalidOperationException>(() => service.SaveRecord(entity, "", 0, "admin", (db, code) =>
        { Persist(db, entity, code, "Rollback"); db.SaveChanges(); throw new InvalidOperationException("Injected failure after insert"); }));
        using var check = fixture.CreateDbContext(); Assert.Empty(check.Customers); Assert.Empty(check.Products);
        Assert.Equal(1, service.Load(entity).NextNumber);
        Assert.DoesNotContain(check.Settings, item => item.Key == (entity == "Customer" ? "customerCodeNextNumber" : "productCodeNextNumber"));
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public async Task ConcurrentIndependentContexts_NeverProduceDuplicateCodes(string entity)
    {
        using var fixture = new TestDatabaseFixture(); fixture.CreateModel();
        var tasks = Enumerable.Range(0, 8).Select(index => Task.Run(() => new AutoCodeGenerator(fixture).SaveRecord(entity, "", 0, "admin", (db, code) =>
        { var entry = Persist(db, entity, code, "Concurrent " + index); db.SaveChanges(); return entry is Customer customer ? customer.Id : ((Product)entry).Id; }))).ToArray();
        var ids = await Task.WhenAll(tasks); Assert.Equal(8, ids.Distinct().Count());
        using var check = fixture.CreateDbContext();
        var codes = entity == "Customer" ? check.Customers.Select(item => item.CustomerCode).ToArray() : check.Products.Select(item => item.ProductCode).ToArray();
        Assert.Equal(8, codes.Distinct().Count()); Assert.Equal(9, new AutoCodeGenerator(fixture).Load(entity).NextNumber);
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void DatabaseIndex_RejectsDirectDuplicateCodes(string entity)
    {
        using var fixture = new TestDatabaseFixture(); using var db = fixture.CreateDbContext();
        Persist(db, entity, "SAME", "One"); db.SaveChanges(); Persist(db, entity, "SAME", "Two");
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact] [Trait("Category", "Integration")]
    public async Task RestrictedUser_CannotAllocateOrChangeSettings()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Viewer"); TestData.CreateUser(model, "viewer", "Viewer"); TestData.SetPermissions(fixture, "Viewer", "Customer", "View");
        var generator = new AutoCodeGenerator(fixture);
        Assert.Throws<UnauthorizedAccessException>(() => generator.SaveRecord("Customer", "", 0, "viewer", (_, _) => throw new Exception("Must not execute")));
        Assert.Throws<UnauthorizedAccessException>(() => generator.SaveSettings(new Dictionary<string, AutoCodeSettings> { ["Customer"] = new("X") }, "viewer"));
        using var check = fixture.CreateDbContext(); Assert.Empty(check.Customers);
    }

    internal static FormField[] Fields(string entity, string name, string code = "")
    {
        var fields = entity == "Customer" ? FormCatalog.Customer() : FormCatalog.Product();
        fields.Single(field => field.Label == "Name").Value = name;
        // Customer phone is required by the UI; use a stable unique number per name.
        if (entity == "Customer") fields.Single(field => field.Label == "Phone").Value = Math.Abs((long)StringComparer.Ordinal.GetHashCode(name)).ToString();
        fields.Single(field => field.Label == entity + " ID").Value = code;
        return fields;
    }
    private static object Persist(LedgerNestDbContext db, string entity, string code, string name)
    {
        if (entity == "Customer") { var customer = new Customer { Name = name, CustomerCode = code }; db.Customers.Add(customer); return customer; }
        var product = new Product { Name = name, ProductCode = code }; db.Products.Add(product); return product;
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void EditingWithBlankCode_PreservesExistingCodeAndSequence(string entity)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        Assert.True(model.SaveRecord(entity, Fields(entity, "Original", "LEGACY-001")));
        var existing = (entity == "Customer" ? model.Customers : model.Products).Single();
        Assert.True(model.SaveRecord(entity, Fields(entity, "Edited"), existing), model.Status);
        Assert.Equal("LEGACY-001", (entity == "Customer" ? model.Customers : model.Products).Single()[entity + " ID"]);
        Assert.Equal(1, new AutoCodeGenerator(fixture).Load(entity).NextNumber);
    }

    [Fact] [Trait("Category", "Integration")]
    public void CorruptStoredConfiguration_IsReportedAndCannotBeOverwrittenFromFallbackUi()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext()) { db.Settings.Add(new AppSetting { Key = "customerCodeNextNumber", Value = "broken" }); db.SaveChanges(); }
        var viewModel = model.CreateMasterCodeSettings(); Assert.NotEmpty(viewModel.Error); Assert.False(viewModel.CanSave);
        viewModel.SaveCommand.Execute(null);
        using var check = fixture.CreateDbContext(); Assert.Equal("broken", check.Settings.Find("customerCodeNextNumber")!.Value);
        Assert.False(model.SaveRecord("Customer", Fields("Customer", "Rejected"))); Assert.Empty(check.Customers);
    }

    [Fact] [Trait("Category", "Integration")]
    public void DatabaseInsertFailure_DoesNotConsumeCodeOrAddRecord()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext()) db.Database.ExecuteSqlRaw("CREATE TRIGGER fail_customer BEFORE INSERT ON customers BEGIN SELECT RAISE(ABORT, 'Injected insert failure'); END");
        Assert.False(model.SaveRecord("Customer", Fields("Customer", "Failed")));
        using var check = fixture.CreateDbContext(); Assert.Empty(check.Customers); Assert.Equal(1, new AutoCodeGenerator(fixture).Load("Customer").NextNumber);
        check.Database.ExecuteSqlRaw("DROP TRIGGER fail_customer");
        Assert.True(model.SaveRecord("Customer", Fields("Customer", "Success")), model.Status); Assert.Equal("CUST00001", model.Customers.Single()["Customer ID"]);
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void EmptyPrefixAndInvalidSettings_AreHandledWithoutChangingExistingData(string entity)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(); var generator = new AutoCodeGenerator(fixture);
        generator.SaveSettings(new Dictionary<string, AutoCodeSettings> { [entity] = new("", 25, 0) }, "admin");
        Assert.True(model.SaveRecord(entity, Fields(entity, "Numeric"))); Assert.Equal("25", (entity == "Customer" ? model.Customers : model.Products).Single()[entity + " ID"]);
        Assert.ThrowsAny<ArgumentException>(() => generator.SaveSettings(new Dictionary<string, AutoCodeSettings> { [entity] = new("X", 0, 5) }, "admin"));
        Assert.ThrowsAny<ArgumentException>(() => generator.SaveSettings(new Dictionary<string, AutoCodeSettings> { [entity] = new("X", -1, 5) }, "admin"));
        Assert.Equal("", generator.Load(entity).Prefix); Assert.Equal(26, generator.Load(entity).NextNumber);
    }

    [Fact] [Trait("Category", "Integration")]
    public async Task SettingsUpdateWithoutView_IsDeniedAtServiceBoundary()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        await TestData.CreateRole(model, "BlindEditor"); TestData.CreateUser(model, "blind", "BlindEditor");
        TestData.SetPermissions(fixture, "BlindEditor", "Settings", "Update");
        Assert.Throws<UnauthorizedAccessException>(() => new AutoCodeGenerator(fixture).SaveSettings(new Dictionary<string, AutoCodeSettings> { ["Customer"] = new("X") }, "blind"));
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void MultipleLegacyNullCodes_RemainNullWhenEditing(string entity)
    {
        using var fixture = new TestDatabaseFixture(); fixture.CreateModel();
        using (var db = fixture.CreateDbContext())
        {
            if (entity == "Customer") db.Customers.AddRange(new Customer { Name = "Old one", Phone = "111" }, new Customer { Name = "Old two", Phone = "222" });
            else db.Products.AddRange(new Product { Name = "Old one" }, new Product { Name = "Old two" });
            db.SaveChanges();
        }
        var model = fixture.CreateModel(); var records = (entity == "Customer" ? model.Customers : model.Products).ToArray();
        foreach (var record in records) Assert.True(model.SaveRecord(entity, Fields(entity, record.Name + " edited"), record), model.Status);
        using var check = fixture.CreateDbContext();
        var codes = entity == "Customer" ? check.Customers.Select(item => item.CustomerCode).ToArray() : check.Products.Select(item => item.ProductCode).ToArray();
        Assert.All(codes, code => Assert.Null(code)); Assert.Equal(1, new AutoCodeGenerator(fixture).Load(entity).NextNumber);
    }

    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public void IntentionalManualCodes_RetainExistingFormLengthCompatibility(string entity)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        var code = new string('X', 150);
        var fields = Fields(entity, "Manual long code");
        fields.Single(item => item.Label == entity + " ID").Value = code;
        Assert.True(model.SaveRecord(entity, fields), model.Status);
        Assert.Equal(code, (entity == "Customer" ? model.Customers : model.Products).Single()[entity + " ID"]);
        Assert.Equal(1, new AutoCodeGenerator(fixture).Load(entity).NextNumber);
    }
}
