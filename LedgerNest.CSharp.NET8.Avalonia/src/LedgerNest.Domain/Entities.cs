namespace LedgerNest.Domain;

public sealed class Customer
{
    public int Id { get; set; }
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
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount => GrandTotal - PaidAmount;
    public List<InvoiceItem> Items { get; set; } = [];
}

public sealed class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? ProductId { get; set; }
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
    InvoiceAdditionalCost[] AdditionalCosts);

public sealed record InvoiceCustomerSnapshot(
    string Name, string BusinessName, string Phone, string Email, string GstNumber, string Address);

public sealed record InvoiceAdditionalCost(string Description, decimal Amount);
