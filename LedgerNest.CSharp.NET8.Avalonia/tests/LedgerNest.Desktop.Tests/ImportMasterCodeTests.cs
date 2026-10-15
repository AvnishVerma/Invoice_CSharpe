using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests;

public sealed class ImportMasterCodeTests
{
    [Theory]
    [InlineData("Customer")] [InlineData("Product")]
    public void ImportGeneratesAndPersistsSequentialIdsWithoutChangingExistingIds(string kind)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        string Csv(string rows, bool id = false) => kind == "Customer" ? "name,phone" + (id ? ",customer_id" : "") + "\n" + rows : "name,price" + (id ? ",product_id" : "") + "\n" + rows;
        var first = kind == "Customer" ? "First,1111111111" : "First,10";
        var second = kind == "Customer" ? "Second,2222222222" : "Second,20";
        Assert.Equal(2,model.ImportCsv(kind,Csv(first + "\n" + second)));
        var records = kind == "Customer" ? model.Customers : model.Products;
        var prefix = kind == "Customer" ? "CUST" : "PROD";
        Assert.Equal(prefix + "00001",records.Single(row => row.Name == "First")[kind + " ID"]);
        Assert.Equal(prefix + "00002",records.Single(row => row.Name == "Second")[kind + " ID"]);
        Assert.Equal(1,model.ImportCsv(kind,Csv(first))); // Duplicate updates retain identity and sequence.
        Assert.Equal(3,new AutoCodeGenerator(fixture).Load(kind).NextNumber);
        var manual = kind == "Customer" ? "Manual,3333333333,OLD-9" : "Manual,30,OLD-9";
        Assert.Equal(1,model.ImportCsv(kind,Csv(manual,true)));
        Assert.Equal("OLD-9",records.Single(row => row.Name == "Manual")[kind + " ID"]);
        var reloaded = fixture.CreateModel(); var saved = kind == "Customer" ? reloaded.Customers : reloaded.Products;
        Assert.Equal(prefix + "00001",saved.Single(row => row.Name == "First")[kind + " ID"]);
    }
    [Theory]
    [InlineData("Customer")] [InlineData("Product")]
    public void ImportAssignsMissingLegacyIdAndHonorsConfiguredSequence(string kind)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext())
        {
            if(kind == "Customer") db.Customers.Add(new Customer { Name = "Legacy", Phone = "4444444444", CustomerCode = "" });
            else db.Products.Add(new Product { Name = "Legacy", ProductCode = "" });
            db.SaveChanges();
        }
        var generator = new AutoCodeGenerator(fixture);
        generator.SaveSettings(new Dictionary<string,AutoCodeSettings> { [kind] = new("ID-",25,4,true) },"admin");
        model = fixture.CreateModel();
        Assert.Equal(1,model.ImportCsv(kind,kind == "Customer" ? "name,phone\nLegacy,4444444444" : "name,price\nLegacy,10"));
        var records = kind == "Customer" ? model.Customers : model.Products;
        Assert.Equal("ID-0025",records.Single(row => row.Name == "Legacy")[kind + " ID"]);
        Assert.Equal(26,generator.Load(kind).NextNumber);
    }
    [Theory]
    [InlineData("Customer")] [InlineData("Product")]
    public void DisabledGenerationRequiresSuppliedIdAndFailedRowsDoNotAdvanceSequence(string kind)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        var generator = new AutoCodeGenerator(fixture);
        generator.SaveSettings(new Dictionary<string,AutoCodeSettings> { [kind] = new("AUTO-",1,3,false) },"admin");
        var header = kind == "Customer" ? "name,phone" : "name,price";
        var row = kind == "Customer" ? "Manual,5555555555" : "Manual,10";
        Assert.Equal(0,model.ImportCsv(kind,header + "\n" + row));
        Assert.Equal(1,generator.Load(kind).NextNumber);
        var codeHeader = kind == "Customer" ? "customer_code" : "product_code";
        Assert.Equal(1,model.ImportCsv(kind,header + "," + codeHeader + "\n" + row + ",MANUAL-1"));
        var duplicate = kind == "Customer" ? "Other,6666666666,MANUAL-1" : "Other,20,MANUAL-1";
        Assert.Equal(0,model.ImportCsv(kind,header + "," + codeHeader + "\n" + duplicate));
        Assert.Equal(1,generator.Load(kind).NextNumber);
    }

}
