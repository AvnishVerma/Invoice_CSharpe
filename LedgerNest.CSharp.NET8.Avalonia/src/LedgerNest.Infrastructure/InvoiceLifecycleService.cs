using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Infrastructure;

public sealed class InvoiceLifecycleService(IDbContextFactory<LedgerNestDbContext> factory)
{
    // Legacy editor commands are synchronous. Run the async service without capturing
    // Avalonia's synchronization context while those commands are being migrated.
    public Invoice Void(int invoiceId, string username, string reason) =>
        Task.Run(() => VoidAsync(invoiceId, username, reason)).GetAwaiter().GetResult();

    public async Task<Invoice> VoidAsync(int invoiceId, string username, string reason, CancellationToken cancellationToken = default)
    {
        var authorization = new AuthorizationService(factory);
        if (!await authorization.IsUserAllowedAsync(username, "Invoice", "View", cancellationToken)
            || !await authorization.IsUserAllowedAsync(username, "Invoice", "Update", cancellationToken))
            throw new UnauthorizedAccessException("You do not have permission to void invoices.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A void reason is required.", nameof(reason));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.EnsureCurrentSchema();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await db.Invoices.Include(item => item.Items).SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice is unavailable or in trash.");
        InvoiceStatusRules.ValidateVoid(invoice, await db.Payments.AnyAsync(payment => payment.InvoiceId == invoice.Id, cancellationToken));

        // Reverse actual recorded sales, not current product settings or inferred item quantities.
        // Older documents with no stock ledger must not manufacture inventory on void.
        var movements = await db.InventoryTransactions.Where(movement => movement.SourceType == "Invoice"
            && movement.Reference == invoice.InvoiceNumber).ToArrayAsync(cancellationToken);
        foreach (var movement in movements)
        {
            var product = await db.Products.SingleOrDefaultAsync(item => item.Id == movement.ProductId, cancellationToken)
                ?? throw new InvalidOperationException("A product in this invoice's stock history is unavailable.");
            product.StockQuantity -= movement.BaseQuantityChange;
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = product.Id, TransactionType = "Invoice Void", BaseQuantityChange = -movement.BaseQuantityChange,
                SourceType = "InvoiceVoid", SourceId = invoice.Id, Reference = invoice.InvoiceNumber,
                Notes = reason.Trim(), CreatedBy = username
            });
        }
        invoice.Status = "Voided";
        invoice.CancellationReason = reason.Trim();
        invoice.CancelledBy = username;
        invoice.CancelledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return invoice;
    }
}
