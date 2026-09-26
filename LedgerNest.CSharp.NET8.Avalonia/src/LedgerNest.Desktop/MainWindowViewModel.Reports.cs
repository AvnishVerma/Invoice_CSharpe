using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    // Performs the build report action for this screen or workflow.
    public ReportSnapshot BuildReport(string name)
    {
        var invoices = ActiveInvoices.ToArray();
        var billed = invoices.Sum(i => ParseDecimal(i["Total"]));
        var paid = invoices.Sum(i => ParseDecimal(i["Paid"]));
        var outstanding = invoices.Sum(i => ParseDecimal(i["Outstanding"]));
        var rows = name switch
        {
            "Revenue" => new[] {
                new[] { "Metric", "Value" },
                new[] { "Total Billed", Money(billed) },
                new[] { "Total Collected", Money(paid) },
                new[] { "Outstanding", Money(outstanding) },
                new[] { "Average Invoice Value", Money(invoices.Length == 0 ? 0 : billed / invoices.Length) } },
            "Receivables" => invoices.Where(i => ParseDecimal(i["Outstanding"]) > 0).Select(i => new[] { i["Customer"], i.Name, i["Date"], Money(ParseDecimal(i["Outstanding"])) }).Prepend(["Customer", "Invoice ID", "Date", "Outstanding"]).ToArray(),
            "Tax" => invoices.GroupBy(i => i["Date"]).Select(g => new[] { g.Key, Money(g.Sum(i => ParseDecimal(i["Tax"]))) }).OrderBy(r => r[0]).Prepend(["Date", "Tax"]).ToArray(),
            "Customers" => invoices.GroupBy(i => i["Customer"]).OrderByDescending(g => g.Sum(i => ParseDecimal(i["Total"]))).Select(g => new[] { string.IsNullOrWhiteSpace(g.Key) ? "Unknown" : g.Key, g.Count().ToString(), Money(g.Sum(i => ParseDecimal(i["Total"]))), Money(g.Sum(i => ParseDecimal(i["Paid"]))), Money(g.Sum(i => ParseDecimal(i["Outstanding"]))) }).Prepend(["Customer", "Invoices", "Billed", "Collected", "Outstanding"]).ToArray(),
            "Products" => ProductReportRows(),
            "Inventory" => InventoryReportRows(),
            "Quotations" => new string[][] { ["Metric", "Value"], ["Quotations Issued", Invoices.Count(i => i["Type"] == "Quotation" && !DeletedRecords.Contains(i.Id)).ToString()], ["Invoices in Period", invoices.Length.ToString()] },
            "Invoice Status" => invoices.Select(i => new[] { i.Name, i["Customer"], i["Date"], Money(ParseDecimal(i["Total"])), i["Status"], Money(ParseDecimal(i["Outstanding"])) }).Prepend(["Invoice", "Customer", "Date", "Total", "Status", "Outstanding"]).ToArray(),
            "Daily Report" => DailyReportRows(),
            _ => new string[][] { ["Metric", "Value"] }
        };

        return new ReportSnapshot(name, invoices.Length, billed, paid, outstanding, rows);
    }

    // Builds payment-status counts and customer aging buckets for the receivables report.
    public ReceivablesReportSnapshot BuildReceivablesReport()
    {
        var invoices = ActiveInvoices.ToArray();
        Dictionary<int, DateTime?> persistedDueDates = [];
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            persistedDueDates = db.Invoices.AsNoTracking()
                .Where(invoice => invoice.DeletedAt == null && invoice.Type == "Invoice")
                .ToDictionary(invoice => invoice.Id, invoice => invoice.Snapshot == null ? null : invoice.Snapshot.DueDate);
        }

        var rows = invoices
            .Where(invoice => ParseDecimal(invoice["Outstanding"]) > .005m)
            .Select(invoice =>
            {
                var dueDate = persistedDueDates.GetValueOrDefault(invoice.SourceId);
                if (dueDate == null && DateTime.TryParse(invoice["Due Date"], out var parsedDueDate)) dueDate = parsedDueDate;
                var outstanding = ParseDecimal(invoice["Outstanding"]);
                var daysOverdue = dueDate.HasValue ? Math.Max(0, (DateTime.Today - dueDate.Value.Date).Days) : (int?)null;
                var bucket = dueDate switch
                {
                    null => ReceivableAgingBucket.NoDueDate,
                    _ when dueDate.Value.Date >= DateTime.Today => ReceivableAgingBucket.Current,
                    _ when daysOverdue <= 30 => ReceivableAgingBucket.Days0To30,
                    _ when daysOverdue <= 60 => ReceivableAgingBucket.Days31To60,
                    _ when daysOverdue <= 90 => ReceivableAgingBucket.Days61To90,
                    _ => ReceivableAgingBucket.Days90Plus
                };
                return new AgedReceivableSnapshot(
                    string.IsNullOrWhiteSpace(invoice["Customer"]) ? "Cash" : invoice["Customer"],
                    invoice.Name,
                    outstanding,
                    daysOverdue,
                    bucket);
            })
            .OrderByDescending(row => row.DaysOverdue ?? -1)
            .ThenBy(row => row.Customer)
            .ToArray();

        var summaries = rows.GroupBy(row => row.Customer)
            .Select(group => new ReceivableAgingSummarySnapshot(
                group.Key,
                group.Where(row => row.Bucket == ReceivableAgingBucket.Current).Sum(row => row.Outstanding),
                group.Where(row => row.Bucket == ReceivableAgingBucket.Days0To30).Sum(row => row.Outstanding),
                group.Where(row => row.Bucket == ReceivableAgingBucket.Days31To60).Sum(row => row.Outstanding),
                group.Where(row => row.Bucket == ReceivableAgingBucket.Days61To90).Sum(row => row.Outstanding),
                group.Where(row => row.Bucket == ReceivableAgingBucket.Days90Plus).Sum(row => row.Outstanding),
                group.Where(row => row.Bucket == ReceivableAgingBucket.NoDueDate).Sum(row => row.Outstanding)))
            .OrderBy(row => row.Customer)
            .ToArray();

        var paid = invoices.Count(invoice => ParseDecimal(invoice["Outstanding"]) <= .005m);
        var partial = invoices.Count(invoice => ParseDecimal(invoice["Outstanding"]) > .005m && ParseDecimal(invoice["Paid"]) > .005m);
        var unpaid = invoices.Length - paid - partial;
        return new ReceivablesReportSnapshot(invoices.Length, paid, partial, unpaid, summaries, rows);
    }

    // Builds customer revenue rankings and statement ledgers from active invoices and recorded payments.
    public CustomerReportSnapshot BuildCustomerReport()
    {
        var invoices = ActiveInvoices.ToArray();
        var customerNames = Customers.Select(customer => customer.Name)
            .Concat(invoices.Select(invoice => invoice["Customer"]))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();

        var customers = customerNames.Select(customerName =>
        {
            var customerInvoices = invoices
                .Where(invoice => invoice["Customer"].Equals(customerName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(invoice => DateTime.TryParse(invoice["Date"], out var date) ? date : DateTime.MinValue)
                .ThenBy(invoice => invoice.Name)
                .ToArray();
            var events = new List<CustomerStatementTransactionSnapshot>();
            foreach (var invoice in customerInvoices)
            {
                var invoiceDate = DateTime.TryParse(invoice["Date"], out var parsedInvoiceDate) ? parsedInvoiceDate : DateTime.Today;
                events.Add(new CustomerStatementTransactionSnapshot(invoiceDate, "Invoice", invoice.Name, "Invoice raised", ParseDecimal(invoice["Total"]), 0m, 0m));
                foreach (var payment in Payments.Where(payment => payment["InvoiceId"] == invoice.SourceId.ToString()))
                {
                    var paymentDate = DateTime.TryParse(payment["Date"], out var parsedPaymentDate) ? parsedPaymentDate : invoiceDate;
                    var method = string.IsNullOrWhiteSpace(payment["Method"]) ? "" : $" · {payment["Method"]}";
                    events.Add(new CustomerStatementTransactionSnapshot(paymentDate, "Payment", payment.Name, $"Payment received{method}", 0m, ParseDecimal(payment["Amount"]), 0m));
                }
            }

            decimal balance = 0;
            var transactions = events.OrderBy(entry => entry.Date).ThenBy(entry => entry.Type == "Invoice" ? 0 : 1)
                .ThenBy(entry => entry.Reference)
                .Select(entry =>
                {
                    balance += entry.Debit - entry.Credit;
                    return entry with { Balance = balance };
                })
                .ToArray();
            var overdue = customerInvoices.Where(invoice =>
                    ParseDecimal(invoice["Outstanding"]) > .005m
                    && DateTime.TryParse(invoice["Due Date"], out var dueDate)
                    && dueDate.Date < DateTime.Today)
                .Sum(invoice => ParseDecimal(invoice["Outstanding"]));
            return new CustomerReportCustomerSnapshot(
                customerName,
                customerInvoices.Length,
                customerInvoices.Sum(invoice => ParseDecimal(invoice["Total"])),
                customerInvoices.Sum(invoice => ParseDecimal(invoice["Paid"])),
                customerInvoices.Sum(invoice => ParseDecimal(invoice["Outstanding"])),
                overdue,
                transactions);
        }).ToArray();

        return new CustomerReportSnapshot(
            customers.Where(customer => customer.InvoiceCount > 0).OrderByDescending(customer => customer.Billed).ThenBy(customer => customer.Name).ToArray(),
            customers);
    }

    // Builds the six-month revenue summary, profit metrics, and monthly chart/table data.
    public RevenueReportSnapshot BuildRevenueReport(int monthCount = 6)
    {
        monthCount = Math.Max(1, monthCount);
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1 - monthCount);
        var end = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

        if (dbFactory == null)
        {
            var records = ActiveInvoices
                .Select(record => new { Record = record, Date = DateTime.TryParse(record["Date"], out var date) ? date : DateTime.Today })
                .Where(entry => entry.Date >= start && entry.Date < end)
                .ToArray();
            var months = records.GroupBy(entry => new DateTime(entry.Date.Year, entry.Date.Month, 1))
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var billed = group.Sum(entry => ParseDecimal(entry.Record["Total"]));
                    var collected = group.Sum(entry => ParseDecimal(entry.Record["Paid"]));
                    var outstanding = group.Sum(entry => ParseDecimal(entry.Record["Outstanding"]));
                    var cogs = group.Sum(entry => ParseDecimal(entry.Record["COGS"]));
                    var profit = group.Sum(entry => ParseDecimal(entry.Record["Profit"]));
                    return new RevenueMonthSnapshot(group.Key, group.Count(), billed, collected, outstanding, cogs, profit);
                })
                .ToArray();
            var totalBilled = records.Sum(entry => ParseDecimal(entry.Record["Total"]));
            var totalCollected = records.Sum(entry => ParseDecimal(entry.Record["Paid"]));
            var totalOutstanding = records.Sum(entry => ParseDecimal(entry.Record["Outstanding"]));
            var totalProfit = months.Sum(month => month.Profit);
            var realizedProfit = records.Where(entry => ParseDecimal(entry.Record["Outstanding"]) <= .005m).Sum(entry => ParseDecimal(entry.Record["Profit"]));
            return new RevenueReportSnapshot(records.Length, totalBilled, totalCollected, totalOutstanding,
                records.Length == 0 ? 0 : totalBilled / records.Length, totalProfit, realizedProfit, 0, months);
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var invoices = db.Invoices.AsNoTracking()
            .Include(invoice => invoice.Items)
            .Where(invoice => invoice.DeletedAt == null && invoice.Type == "Invoice" && invoice.InvoiceDate >= start && invoice.InvoiceDate < end)
            .ToArray();
        var calculated = invoices.Select(invoice =>
        {
            var revenue = invoice.Items.Sum(item => RevenueItemNet(invoice, item));
            var cogs = invoice.Items.Sum(item => item.Quantity * item.PurchasePrice);
            return new RevenueInvoiceCalculation(invoice, revenue, cogs, revenue - cogs);
        }).ToArray();
        var monthly = calculated.GroupBy(entry => new DateTime(entry.Invoice.InvoiceDate.Year, entry.Invoice.InvoiceDate.Month, 1))
            .OrderBy(group => group.Key)
            .Select(group => new RevenueMonthSnapshot(
                group.Key,
                group.Count(),
                group.Sum(entry => entry.Invoice.GrandTotal),
                group.Sum(entry => entry.Invoice.PaidAmount),
                group.Sum(entry => entry.Invoice.BalanceAmount),
                group.Sum(entry => entry.Cogs),
                group.Sum(entry => entry.Profit)))
            .ToArray();
        var billed = invoices.Sum(invoice => invoice.GrandTotal);
        var collected = invoices.Sum(invoice => invoice.PaidAmount);
        var outstanding = invoices.Sum(invoice => invoice.BalanceAmount);
        var profit = calculated.Sum(entry => entry.Profit);
        var realized = calculated.Where(entry => entry.Invoice.BalanceAmount <= .005m).Sum(entry => entry.Profit);
        var missingCostItems = invoices.SelectMany(invoice => invoice.Items).Count(item => item.PurchasePrice <= 0);
        return new RevenueReportSnapshot(invoices.Length, billed, collected, outstanding,
            invoices.Length == 0 ? 0 : billed / invoices.Length, profit, realized, missingCostItems, monthly);
    }

    // Calculates tax-exclusive item revenue using the invoice's saved tax mode.
    private static decimal RevenueItemNet(Invoice invoice, InvoiceItem item)
    {
        var discount = item.DiscountPerUnit ? item.Discount * item.Quantity : item.Discount;
        var net = item.UnitPrice * item.Quantity - discount + item.ExtraCost;
        var perItemTax = invoice.Snapshot == null || invoice.Snapshot.TaxMode.Equals("Per Item", StringComparison.OrdinalIgnoreCase);
        return perItemTax && item.PriceIncludesTax && item.TaxRate > 0 ? net / (1 + item.TaxRate / 100) : net;
    }


    // Performs the product report rows action for this screen or workflow.
    private string[][] ProductReportRows()
    {
        var report = BuildProductReport();
        return report.Products.Select(product => new[]
            {
                product.Name,
                product.UnitsSold.ToString("0.###"),
                Money(product.Revenue),
                Money(product.DiscountGiven),
                Money(product.Profit),
                product.Margin.ToString("0.#") + "%"
            })
            .Prepend(["Product / Service", "Units Sold", "Revenue", "Discount Given", "Profit", "Margin"])
            .ToArray();
    }

    // Builds ranked product and service sales totals for the last three months.
    public ProductReportSnapshot BuildProductReport()
    {
        IEnumerable<ProductReportLine> lines;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            lines = db.InvoiceItems.AsNoTracking()
                .Join(db.Invoices.AsNoTracking(), item => item.InvoiceId, invoice => invoice.Id, (item, invoice) => new { item, invoice })
                .Where(x => x.invoice.Status != "Draft" && x.invoice.Type == "Invoice" && x.invoice.DeletedAt == null && x.invoice.InvoiceDate >= DateTime.Today.AddMonths(-3))
                .Select(x => new ProductReportLine(x.item.Description, x.item.Quantity, x.item.UnitPrice, x.item.DiscountPerUnit ? x.item.Discount * x.item.Quantity : x.item.Discount, x.item.PurchasePrice))
                .ToArray();
        }
        else
        {
            lines = Lines.Select(line => new ProductReportLine(line.Name, line.Quantity, line.Price, line.DiscountPerUnit ? line.Discount * line.Quantity : line.Discount, 0)).ToArray();
        }

        var materialized = lines.ToArray();
        var products = materialized.GroupBy(line => line.Name)
            .Select(group =>
            {
                var revenue = group.Sum(line => line.UnitPrice * line.Quantity);
                var discount = group.Sum(line => line.Discount);
                var cost = group.Sum(line => line.PurchasePrice * line.Quantity);
                var profit = revenue - discount - cost;
                return new ProductReportProductSnapshot(
                    group.Key,
                    group.Sum(line => line.Quantity),
                    revenue,
                    discount,
                    profit,
                    revenue == 0 ? 0 : profit * 100 / revenue);
            })
            .OrderByDescending(product => product.Revenue)
            .ThenBy(product => product.Name)
            .ToArray();
        return new ProductReportSnapshot(products, materialized.Count(line => line.PurchasePrice <= 0));
    }

    // Builds invoice status rows with saved totals, payment balances, and overdue state.
    public InvoiceStatusReportSnapshot BuildInvoiceStatusReport()
    {
        var rows = ActiveInvoices.Select(invoice =>
        {
            var date = DateTime.TryParse(invoice["Date"], out var parsedDate) ? parsedDate.Date : DateTime.Today;
            var dueDate = DateTime.TryParse(invoice["Due Date"], out var parsedDueDate) ? parsedDueDate.Date : (DateTime?)null;
            var total = ParseDecimal(invoice["Total"]);
            var paid = ParseDecimal(invoice["Paid"]);
            var outstanding = ParseDecimal(invoice["Outstanding"]);
            var status = outstanding <= .005m ? "Paid" : paid > .005m ? "Partial" : "Unpaid";
            return new InvoiceStatusRowSnapshot(
                date,
                invoice.Name,
                string.IsNullOrWhiteSpace(invoice["Customer"]) ? "Cash" : invoice["Customer"],
                total,
                paid,
                outstanding,
                status,
                outstanding > .005m && dueDate.HasValue && dueDate.Value < DateTime.Today);
        }).OrderByDescending(row => row.Date).ThenByDescending(row => row.InvoiceId).ToArray();
        return new InvoiceStatusReportSnapshot(rows);
    }

    // Builds daily sales, cost, profit, and missing-cost totals from saved invoice snapshots.
    public DailySalesReportSnapshot BuildDailySalesReport()
    {
        if (dbFactory == null)
        {
            var days = ActiveInvoices.GroupBy(invoice => DateTime.TryParse(invoice["Date"], out var date) ? date.Date : DateTime.Today)
                .Select(group => new DailySalesDaySnapshot(
                    group.Key,
                    group.Count(),
                    group.Sum(invoice => ParseDecimal(invoice["Total"])),
                    group.Sum(invoice => ParseDecimal(invoice["COGS"])),
                    group.Sum(invoice => ParseDecimal(invoice["Profit"])),
                    0))
                .OrderBy(day => day.Date)
                .ToArray();
            return new DailySalesReportSnapshot(days);
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var invoices = db.Invoices.AsNoTracking().Include(invoice => invoice.Items)
            .Where(invoice => invoice.DeletedAt == null && invoice.Type == "Invoice" && invoice.Status != "Draft")
            .ToArray();
        var daily = invoices.GroupBy(invoice => invoice.InvoiceDate.Date)
            .Select(group =>
            {
                var revenue = group.Sum(invoice => invoice.Items.Sum(item => RevenueItemNet(invoice, item)));
                var cogs = group.Sum(invoice => invoice.Items.Sum(item => item.PurchasePrice * item.Quantity));
                return new DailySalesDaySnapshot(
                    group.Key,
                    group.Count(),
                    group.Sum(invoice => invoice.GrandTotal),
                    cogs,
                    revenue - cogs,
                    group.Sum(invoice => invoice.Items.Count(item => item.PurchasePrice <= 0)));
            })
            .OrderBy(day => day.Date)
            .ToArray();
        return new DailySalesReportSnapshot(daily);
    }

    // Formats daily sales calculations for CSV and PDF report exports.
    private string[][] DailyReportRows() => BuildDailySalesReport().Days
        .Select(day => new[]
        {
            day.Date.ToString("dd/MM/yyyy"),
            day.InvoiceCount.ToString(),
            Money(day.Sales),
            Money(day.Cogs),
            Money(day.Profit),
            (day.Sales == 0 ? 0 : day.Profit * 100 / day.Sales).ToString("0.#") + "%"
        })
        .Prepend(["Date", "Invoices", "Sales", "COGS", "Profit", "Margin"])
        .ToArray();

    // Builds inventory valuation totals from finite-stock product records.
    public InventoryReportSnapshot BuildInventoryReport()
    {
        var included = Products.Where(product => !product["Type"].Equals("Service", StringComparison.OrdinalIgnoreCase)
                && !(bool.TryParse(product["Unlimited stock"], out var unlimited) && unlimited))
            .Select(product =>
            {
                var stock = Math.Max(0, ParseDecimal(product["Stock"]));
                var purchasePrice = Math.Max(0, ParseDecimal(product["Purchase Price"]));
                var salePrice = Math.Max(0, ParseDecimal(product["Sale Price"]));
                return new InventoryProductSnapshot(product.Name, stock, purchasePrice, salePrice, stock * purchasePrice, stock * salePrice);
            })
            .OrderByDescending(product => product.StockValue)
            .ThenBy(product => product.Name)
            .ToArray();
        return new InventoryReportSnapshot(
            included.Sum(product => product.StockValue),
            included.Sum(product => product.SaleValue),
            included.Sum(product => product.SaleValue - product.StockValue),
            included.Sum(product => product.Stock),
            included.Length,
            Products.Count - included.Length,
            included);
    }

    // Formats inventory valuation rows for CSV and PDF exports.
    private string[][] InventoryReportRows() => BuildInventoryReport().Products
        .Select(product => new[] { product.Name, product.Stock.ToString("0.###"), Money(product.PurchasePrice), Money(product.StockValue), Money(product.SaleValue) })
        .Prepend(["Product", "Stock", "Purchase Price", "Stock Value", "Sale Value"])
        .ToArray();

    private sealed record ProductReportLine(string Name, decimal Quantity, decimal UnitPrice, decimal Discount, decimal PurchasePrice);
    private sealed record RevenueInvoiceCalculation(Invoice Invoice, decimal Revenue, decimal Cogs, decimal Profit);


    // Performs the export report pdf action for this screen or workflow.
    public byte[] ExportReportPdf(string name)
    {
        var report = BuildReport(name);
        var lines = new List<string>
        {
            Branding.Name,
            $"{name} Report",
            $"Invoices: {report.InvoiceCount}",
            $"Billed: {Money(report.Billed)}",
            $"Collected: {Money(report.Collected)}",
            $"Outstanding: {Money(report.Outstanding)}",
            ""
        };

        foreach (var row in report.Rows) lines.Add(string.Join("  |  ", row));
        Status = $"Exported {name} report PDF.";
        return SimplePdf.Create(lines);
    }

    // Performs the export report csv action for this screen or workflow.
    public string ExportReportCsv(string name)
    {
        var rows = BuildReport(name).Rows;
        Status = $"Exported {name} report.";
        return string.Join(Environment.NewLine, rows.Select(row => string.Join(",", row.Select(EscapeCsv)))) + Environment.NewLine;
    }

}

public sealed record ReportSnapshot(string Name, int InvoiceCount, decimal Billed, decimal Collected, decimal Outstanding, string[][] Rows);

// Identifies the aging column that contains an outstanding invoice balance.
public enum ReceivableAgingBucket
{
    Current,
    Days0To30,
    Days31To60,
    Days61To90,
    Days90Plus,
    NoDueDate
}

// Provides all payment-status and aging data rendered by the receivables report.
public sealed record ReceivablesReportSnapshot(
    int InvoiceCount,
    int PaidCount,
    int PartialCount,
    int UnpaidCount,
    ReceivableAgingSummarySnapshot[] AgingSummaries,
    AgedReceivableSnapshot[] AgedReceivables);

// Provides one customer row in the A/R aging summary.
public sealed record ReceivableAgingSummarySnapshot(
    string Customer,
    decimal Current,
    decimal Days0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days90Plus,
    decimal NoDueDate)
{
    public decimal Total => Current + Days0To30 + Days31To60 + Days61To90 + Days90Plus + NoDueDate;
}

// Provides one outstanding invoice row in the aged receivables table.
public sealed record AgedReceivableSnapshot(
    string Customer,
    string InvoiceId,
    decimal Outstanding,
    int? DaysOverdue,
    ReceivableAgingBucket Bucket);

// Provides customer rankings and selectable customer statements for the customer report.
public sealed record CustomerReportSnapshot(
    CustomerReportCustomerSnapshot[] RevenueCustomers,
    CustomerReportCustomerSnapshot[] StatementCustomers);

// Provides ranked product sales and the count of sold lines with no purchase cost.
public sealed record ProductReportSnapshot(
    ProductReportProductSnapshot[] Products,
    int MissingCostItemCount);

// Provides calculated revenue, cost, and margin values for one product or service.
public sealed record ProductReportProductSnapshot(
    string Name,
    decimal UnitsSold,
    decimal Revenue,
    decimal DiscountGiven,
    decimal Profit,
    decimal Margin);

// Provides invoice rows used by date and payment-status filters.
public sealed record InvoiceStatusReportSnapshot(InvoiceStatusRowSnapshot[] Invoices);

// Provides one invoice's date, customer, amounts, payment status, and overdue state.
public sealed record InvoiceStatusRowSnapshot(
    DateTime Date,
    string InvoiceId,
    string Customer,
    decimal Total,
    decimal Paid,
    decimal Outstanding,
    string Status,
    bool IsOverdue);

// Provides all daily sales rows available to the interactive date filters.
public sealed record DailySalesReportSnapshot(DailySalesDaySnapshot[] Days);

// Provides one day's invoice, sales, cost, profit, and missing-cost totals.
public sealed record DailySalesDaySnapshot(
    DateTime Date,
    int InvoiceCount,
    decimal Sales,
    decimal Cogs,
    decimal Profit,
    int MissingCostItemCount);

// Provides inventory valuation KPIs and finite-stock product rows.
public sealed record InventoryReportSnapshot(
    decimal InventoryValue,
    decimal PotentialSaleValue,
    decimal ProfitLockedInStock,
    decimal TotalUnits,
    int ProductsTracked,
    int ExcludedItemCount,
    InventoryProductSnapshot[] Products);

// Provides stock and valuation amounts for one finite-stock product.
public sealed record InventoryProductSnapshot(
    string Name,
    decimal Stock,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal StockValue,
    decimal SaleValue);

// Provides revenue totals and ledger activity for one customer.
public sealed record CustomerReportCustomerSnapshot(
    string Name,
    int InvoiceCount,
    decimal Billed,
    decimal Collected,
    decimal Outstanding,
    decimal Overdue,
    CustomerStatementTransactionSnapshot[] Transactions);

// Provides one debit or credit entry in a customer statement ledger.
public sealed record CustomerStatementTransactionSnapshot(
    DateTime Date,
    string Type,
    string Reference,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);

// Provides calculated values rendered by the revenue report.
public sealed record RevenueReportSnapshot(
    int InvoiceCount,
    decimal Billed,
    decimal Collected,
    decimal Outstanding,
    decimal AverageInvoiceValue,
    decimal TotalProfit,
    decimal RealizedProfit,
    int MissingCostItemCount,
    RevenueMonthSnapshot[] Months);

// Provides one monthly revenue, collection, cost, and profit breakdown row.
public sealed record RevenueMonthSnapshot(
    DateTime Month,
    int InvoiceCount,
    decimal Billed,
    decimal Collected,
    decimal Outstanding,
    decimal Cogs,
    decimal Profit)
{
    public decimal MarginPercent => Profit + Cogs == 0 ? 0 : Profit * 100 / (Profit + Cogs);
}
