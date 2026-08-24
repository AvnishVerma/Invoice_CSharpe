namespace Invoiso.Domain;

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
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
    public decimal SalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal StockQuantity { get; set; }
}

public sealed class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public int? CustomerId { get; set; }
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
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal => Math.Max(0, Quantity * UnitPrice - Discount);
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
