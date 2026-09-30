using LedgerNest.Domain;

namespace LedgerNest.UnitTests;

public sealed class BusinessRulesTests
{
    [Fact]
    public void MasterNumbersApplyPrefixAndPadding() => Assert.Equal("PRD000001", MasterNumberRules.Format("PRD", 1, 6));

    [Fact]
    public void SellingUnitsConvertAndPriceIndependently()
    {
        Assert.Equal(20m, SellingUnitRules.ToBaseQuantity(2, 10));
        Assert.Equal(190m, SellingUnitRules.Amount(2, 95));
    }

    [Fact]
    public void InventoryLedgerCalculationMatchesMovements() =>
        Assert.Equal(133m, InventoryRules.CurrentStock(100, [50, -20, 5, -2]));

    [Fact]
    public void NegativeInventoryIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => InventoryRules.Apply(5, -6));

    [Theory]
    [InlineData(10, 0, 10, true)]
    [InlineData(10, 5, 5, true)]
    [InlineData(10, 5, 6, false)]
    [InlineData(10, 0, 0, false)]
    [InlineData(10, 0, -1, false)]
    public void RefundEligibility(decimal sold, decimal previous, decimal requested, bool allowed)
    {
        var error = Record.Exception(() => RefundRules.ValidateQuantity(sold, previous, requested));
        Assert.Equal(allowed, error == null);
    }

    [Fact]
    public void RefundTaxPreservesInclusivePrice()
    {
        var result = RefundRules.Calculate(2, 118, 18, true);
        Assert.Equal((200m, 36m, 236m), result);
    }

    [Theory]
    [InlineData("Open", true)]
    [InlineData("Converted", false)]
    [InlineData("Cancelled", false)]
    public void QuotationCancellationUsesStatusRules(string status, bool expected) => Assert.Equal(expected, QuotationRules.CanCancel(status));

    [Fact]
    public void PermissionsAllowAdminAndConfiguredRoleOnly()
    {
        var permissions = new[] { new RolePermission { Role = "Sales", Resource = "Invoice", Action = "Add", IsAllowed = true } };
        Assert.True(PermissionRules.IsAllowed("Admin", "Anything", "Delete", []));
        Assert.True(PermissionRules.IsAllowed("Sales", "Invoice", "Add", permissions));
        Assert.False(PermissionRules.IsAllowed("Sales", "Invoice", "Refund", permissions));
    }

    [Fact]
    public void ProductSearchMatchesAllTermsAndPrioritizesExactCodes()
    {
        var product = new ProductSearchEntry(1, "Blue Ball Pen", "Office Pen", "PRD0007", "PEN-B", "890123", "9608", "Blue ink stationery");
        Assert.Equal(1000, ProductSearchRules.Score(product, "890123"));
        Assert.Equal(1000, ProductSearchRules.Score(product, "PRD0007"));
        Assert.True(ProductSearchRules.Score(product, "blue stationery") > 0);
        Assert.Equal(0, ProductSearchRules.Score(product, "blue laptop"));
    }

    [Fact]
    public void BarcodeLabelsNormalizeAndExpandQuantities()
    {
        var labels = BarcodeRules.Expand([(new BarcodeLabelData("Pen", "ab#12", 10), 2)]);
        Assert.Equal(2, labels.Length);
        Assert.All(labels, label => Assert.Equal("AB-12", label.Code));
        Assert.Throws<InvalidOperationException>(() => BarcodeRules.Expand([(new BarcodeLabelData("Pen", "ABC", 10), 1001)]));
    }
}
