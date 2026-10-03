namespace LedgerNest.Domain;

public sealed class Customer
{
    public int Id { get; set; }
    public string? CustomerCode { get; set; }
    public string Name { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? GstNumber { get; set; }
}

public sealed class Product
{
    public int Id { get; set; }
    public string? ProductCode { get; set; }
    public string? Barcode { get; set; }
    public int? BaseUnitId { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? HsnCode { get; set; }
    public string? Description { get; set; }
    public decimal SalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal StockQuantity { get; set; }
    public string Type { get; set; } = "Product";
    public string AliasName { get; set; } = "";
    public decimal DefaultDiscount { get; set; } = 0;
    public bool PriceIncludesTax { get; set; } = false;
    public bool UnlimitedStock { get; set; } = false;
    public string Unit { get; set; } = "None";
    public string CustomUnit { get; set; } = "";
    public string StorageLocation { get; set; } = "";
    public string ContainerNumber { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public string ExpiryDate { get; set; } = "";
    public string ManufactureDate { get; set; } = "";
    public string ManufacturerName { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string Type { get; set; } = "Invoice";
    public DateTime? DeletedAt { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public InvoiceSnapshot? Snapshot { get; set; }
    public string Status { get; set; } = "Draft";
    public string? CancellationReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount => GrandTotal - PaidAmount <= 0.005m ? 0m : GrandTotal - PaidAmount;
    public List<InvoiceItem> Items { get; set; } = [];
}

public sealed class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? ProductId { get; set; }
    public int? SellingUnitId { get; set; }
    public string SellingUnitCode { get; set; } = "";
    public decimal UnitConversionFactor { get; set; } = 1;
    public decimal BaseQuantity { get; set; }
    public string Description { get; set; } = "";
    public string ProductDescription { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ProductPrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Discount { get; set; }
    public decimal ExtraCost { get; set; }
    public bool DiscountPerUnit { get; set; }
    public bool PriceIncludesTax { get; set; }
    public decimal LineTotal => Math.Max(0, Quantity * UnitPrice - (DiscountPerUnit ? Discount * Quantity : Discount) + ExtraCost);
}

public sealed class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public string? Reference { get; set; }
}

public sealed class CompanyInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? GstNumber { get; set; }
}


public sealed class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Role { get; set; } = "User";
    public bool PasswordChanged { get; set; } = true;
}

public sealed class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class BackupHistoryEntry
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDatabase { get; set; }
}


// Versioned historical input data. Null on records written before snapshot support.
public sealed record InvoiceSnapshot(
    int Version,
    InvoiceCustomerSnapshot Customer,
    DateTime? DueDate,
    string DocumentTitle,
    string CustomInvoiceNumber,
    bool HideInvoiceNumber,
    bool IsInterState,
    string Currency,
    string QuantityLabel,
    string TaxMode,
    decimal TaxRate,
    string DiscountKind,
    decimal DiscountValue,
    string Notes,
    InvoiceAdditionalCost[] AdditionalCosts)
{
    public string[]? LineUnits { get; init; }
    public string[]? LineHsnCodes { get; init; }
    public InvoiceLinePresentation[]? LinePresentations { get; init; }
    public InvoiceCustomFieldValue[]? CustomFields { get; init; }
}

public sealed record InvoiceLinePresentation(string Alias, string ProductType, Dictionary<string, string> Metadata);
public sealed record InvoiceCustomFieldValue(string Id, string Label, string Value);

public sealed record InvoiceCustomerSnapshot(
    string Name, string BusinessName, string Phone, string Email, string GstNumber, string Address);

public sealed record InvoiceAdditionalCost(string Description, decimal Amount);

public sealed class DocumentSequence
{
    public string Type { get; set; } = "Invoice";
    public long NextValue { get; set; } = 1;
}

public sealed class UnitOfMeasure
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ModifiedBy { get; set; } = "";
    public DateTime? ModifiedAt { get; set; }
}

public sealed class ProductSellingUnit
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UnitId { get; set; }
    public decimal ConversionFactor { get; set; } = 1;
    public decimal SellingPrice { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductPrice
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int SellingUnitId { get; set; }
    public string PriceList { get; set; } = "Default";
    public decimal SellingPrice { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ModifiedBy { get; set; } = "";
    public DateTime? ModifiedAt { get; set; }
}

public sealed class NumberSeries
{
    public string EntityType { get; set; } = "";
    public string Prefix { get; set; } = "";
    public long NextValue { get; set; } = 1;
    public int Increment { get; set; } = 1;
    public int NumberLength { get; set; } = 6;
}

public sealed class InventoryTransaction
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string TransactionType { get; set; } = "Adjustment";
    public decimal BaseQuantityChange { get; set; }
    public string SourceType { get; set; } = "";
    public long? SourceId { get; set; }
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class InvoiceRefund
{
    public long Id { get; set; }
    public int InvoiceId { get; set; }
    public string RefundNumber { get; set; } = "";
    public DateTime RefundDate { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "";
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<InvoiceRefundLine> Lines { get; set; } = [];
}

public sealed class InvoiceRefundLine
{
    public long Id { get; set; }
    public long InvoiceRefundId { get; set; }
    public int InvoiceItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class RolePermission
{
    public long Id { get; set; }
    public string Role { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Action { get; set; } = "";
    public bool IsAllowed { get; set; }
}

public sealed class AppRole
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsSystem { get; set; }
}

public sealed class AppUserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}
