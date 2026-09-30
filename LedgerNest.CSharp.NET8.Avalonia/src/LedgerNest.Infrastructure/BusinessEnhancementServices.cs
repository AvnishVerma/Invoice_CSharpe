using System.Data;
using System.Globalization;
using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Infrastructure;

public sealed class NumberSeriesService(IDbContextFactory<LedgerNestDbContext> factory)
{
    public async Task<string> ReserveAsync(string entityType, string prefix, long startingNumber, int increment, int numberLength, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("Entity type is required.", nameof(entityType));
        if (startingNumber < 1 || increment < 1 || numberLength is < 1 or > 18) throw new ArgumentOutOfRangeException(nameof(startingNumber));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var strategy = db.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                    var series = await db.NumberSeries.SingleOrDefaultAsync(item => item.EntityType == entityType, cancellationToken);
                    if (series == null)
                    {
                        series = new NumberSeries { EntityType = entityType, Prefix = prefix.Trim(), NextValue = startingNumber, Increment = increment, NumberLength = numberLength };
                        db.NumberSeries.Add(series);
                    }
                    var value = series.NextValue;
                    var result = MasterNumberRules.Format(series.Prefix, value, series.NumberLength);
                    series.NextValue = checked(value + series.Increment);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < 4) { }
            catch (DbUpdateException) when (attempt < 4) { }
        }
        throw new InvalidOperationException($"Could not reserve a unique {entityType} number after multiple concurrent attempts.");
    }
}

public sealed class UnitMasterService(IDbContextFactory<LedgerNestDbContext> factory)
{
    public async Task<UnitOfMeasure> SaveAsync(UnitOfMeasure unit, string user, string role = "Admin", CancellationToken cancellationToken = default)
    {
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Only administrators can manage units.");
        unit.Code = unit.Code.Trim().ToUpperInvariant();
        unit.Name = unit.Name.Trim();
        if (unit.Code.Length == 0 || unit.Name.Length == 0) throw new InvalidOperationException("Unit code and name are required.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.Units.AnyAsync(item => item.Id != unit.Id && item.Code == unit.Code, cancellationToken)) throw new InvalidOperationException("Unit code already exists.");
        if (unit.Id == 0) { unit.CreatedBy = user; unit.CreatedAt = DateTime.UtcNow; db.Units.Add(unit); }
        else { unit.ModifiedBy = user; unit.ModifiedAt = DateTime.UtcNow; db.Units.Update(unit); }
        await db.SaveChangesAsync(cancellationToken);
        return unit;
    }

    public async Task DeleteAsync(int unitId, string role = "Admin", CancellationToken cancellationToken = default)
    {
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Only administrators can manage units.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var unit = await db.Units.FindAsync([unitId], cancellationToken) ?? throw new InvalidOperationException("Unit does not exist.");
        if (await db.Products.AnyAsync(product => product.BaseUnitId == unitId, cancellationToken) ||
            await db.ProductSellingUnits.AnyAsync(sellingUnit => sellingUnit.UnitId == unitId, cancellationToken))
            throw new InvalidOperationException("Referenced units cannot be deleted. Mark the unit inactive instead.");
        db.Units.Remove(unit);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ProductPricingService(IDbContextFactory<LedgerNestDbContext> factory)
{
    public async Task<decimal> GetPriceAsync(int productId, int sellingUnitId, DateTime effectiveAt, string priceList = "Default", CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var price = await db.ProductPrices.AsNoTracking()
            .Where(item => item.ProductId == productId && item.SellingUnitId == sellingUnitId && item.PriceList == priceList && item.IsActive && item.EffectiveDate <= effectiveAt)
            .OrderByDescending(item => item.EffectiveDate).ThenByDescending(item => item.Id)
            .Select(item => (decimal?)item.SellingPrice).FirstOrDefaultAsync(cancellationToken);
        if (price.HasValue) return price.Value;
        var fallback = await (from sellingUnit in db.ProductSellingUnits.AsNoTracking()
                              join product in db.Products.AsNoTracking() on sellingUnit.ProductId equals product.Id
                              where sellingUnit.Id == sellingUnitId && sellingUnit.ProductId == productId && sellingUnit.IsActive
                              select new { sellingUnit.SellingPrice, ProductSalePrice = product.SalePrice })
            .SingleAsync(cancellationToken);
        return fallback.SellingPrice > 0 ? fallback.SellingPrice : fallback.ProductSalePrice;
    }

    public async Task SaveSellingUnitAsync(ProductSellingUnit sellingUnit, string role = "Admin", CancellationToken cancellationToken = default)
    {
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Only administrators can manage product prices.");
        if (sellingUnit.ConversionFactor <= 0 || sellingUnit.SellingPrice < 0) throw new InvalidOperationException("Conversion and selling price must be valid positive values.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.ProductSellingUnits.AnyAsync(item => item.Id != sellingUnit.Id && item.ProductId == sellingUnit.ProductId && item.UnitId == sellingUnit.UnitId, cancellationToken))
            throw new InvalidOperationException("The selling unit already exists for this product.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (sellingUnit.IsDefault)
            await db.ProductSellingUnits.Where(item => item.ProductId == sellingUnit.ProductId && item.Id != sellingUnit.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false), cancellationToken);
        if (sellingUnit.Id == 0) db.ProductSellingUnits.Add(sellingUnit); else db.ProductSellingUnits.Update(sellingUnit);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProductPrice> SavePriceAsync(ProductPrice price, string user, string role = "Admin", CancellationToken cancellationToken = default)
    {
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Only administrators can manage product prices.");
        if (price.SellingPrice < 0) throw new InvalidOperationException("Selling price cannot be negative.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.ProductSellingUnits.AnyAsync(item => item.Id == price.SellingUnitId && item.ProductId == price.ProductId, cancellationToken))
            throw new InvalidOperationException("Selling unit does not belong to the selected product.");
        price.PriceList = string.IsNullOrWhiteSpace(price.PriceList) ? "Default" : price.PriceList.Trim();
        if (price.Id == 0) { price.CreatedBy = user; price.CreatedAt = DateTime.UtcNow; db.ProductPrices.Add(price); }
        else { price.ModifiedBy = user; price.ModifiedAt = DateTime.UtcNow; db.ProductPrices.Update(price); }
        await db.SaveChangesAsync(cancellationToken);
        return price;
    }
}

public sealed class InventoryService(IDbContextFactory<LedgerNestDbContext> factory)
{
    public async Task PostAsync(InventoryTransaction movement, CancellationToken cancellationToken = default)
    {
        if (movement.BaseQuantityChange == 0) throw new InvalidOperationException("Inventory movement quantity cannot be zero.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var product = await db.Products.SingleAsync(item => item.Id == movement.ProductId, cancellationToken);
            product.StockQuantity = InventoryRules.Apply(product.StockQuantity, movement.BaseQuantityChange, product.UnlimitedStock);
            movement.TransactionDate = movement.TransactionDate == default ? DateTime.UtcNow : movement.TransactionDate;
            movement.CreatedAt = DateTime.UtcNow;
            db.InventoryTransactions.Add(movement);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}

public sealed record RefundLineRequest(int InvoiceItemId, decimal Quantity);

public sealed class InvoiceRefundService(IDbContextFactory<LedgerNestDbContext> factory, NumberSeriesService numberSeries)
{
    public async Task<InvoiceRefund> RefundAsync(int invoiceId, IReadOnlyCollection<RefundLineRequest> requestedLines, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Refund reason is required.");
        if (requestedLines.Count == 0 || requestedLines.Any(line => line.Quantity <= 0)) throw new InvalidOperationException("Select at least one positive refund quantity.");
        var refundNumber = await numberSeries.ReserveAsync("Refund", "REF", 1, 1, 6, cancellationToken);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var invoice = await db.Invoices.Include(item => item.Items).SingleAsync(item => item.Id == invoiceId && item.Type == "Invoice" && item.DeletedAt == null, cancellationToken);
            var previous = await db.InvoiceRefundLines.Where(line => db.InvoiceRefunds.Where(refund => refund.InvoiceId == invoiceId).Select(refund => refund.Id).Contains(line.InvoiceRefundId))
                .GroupBy(line => line.InvoiceItemId).Select(group => new { ItemId = group.Key, Quantity = group.Sum(line => line.Quantity) }).ToDictionaryAsync(item => item.ItemId, item => item.Quantity, cancellationToken);
            var refund = new InvoiceRefund { InvoiceId = invoiceId, RefundNumber = refundNumber, Reason = reason.Trim(), CreatedBy = user, RefundDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            foreach (var request in requestedLines)
            {
                var original = invoice.Items.SingleOrDefault(item => item.Id == request.InvoiceItemId) ?? throw new InvalidOperationException("Refund line does not belong to the selected invoice.");
                RefundRules.ValidateQuantity(original.Quantity, previous.GetValueOrDefault(original.Id), request.Quantity);
                var calculated = RefundRules.Calculate(request.Quantity, original.UnitPrice, original.TaxRate, original.PriceIncludesTax);
                refund.Lines.Add(new InvoiceRefundLine { InvoiceItemId = original.Id, Quantity = request.Quantity, UnitPrice = original.UnitPrice, TaxRate = original.TaxRate, LineTotal = calculated.Total });
                refund.SubTotal += calculated.Subtotal;
                refund.TaxTotal += calculated.Tax;
                if (original.ProductId is int productId)
                {
                    var product = await db.Products.SingleAsync(item => item.Id == productId, cancellationToken);
                    var baseQuantity = SellingUnitRules.ToBaseQuantity(request.Quantity, original.UnitConversionFactor <= 0 ? 1 : original.UnitConversionFactor);
                    if (!product.UnlimitedStock)
                    {
                        product.StockQuantity += baseQuantity;
                        db.InventoryTransactions.Add(new InventoryTransaction { ProductId = productId, TransactionType = "Sales Refund", BaseQuantityChange = baseQuantity, SourceType = "InvoiceRefund", Reference = refundNumber, CreatedBy = user });
                    }
                }
            }
            refund.GrandTotal = refund.Lines.Sum(line => line.LineTotal);
            db.InvoiceRefunds.Add(refund);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return refund;
        });
    }
}

public sealed class AuthorizationService(IDbContextFactory<LedgerNestDbContext> factory)
{
    public async Task<bool> IsAllowedAsync(string role, string resource, string action, CancellationToken cancellationToken = default)
    {
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return true;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var permissions = await db.RolePermissions.AsNoTracking().Where(item => item.Role == role).ToArrayAsync(cancellationToken);
        return PermissionRules.IsAllowed(role, resource, action, permissions);
    }

    public async Task DemandAsync(string role, string resource, string action, CancellationToken cancellationToken = default)
    {
        if (!await IsAllowedAsync(role, resource, action, cancellationToken)) throw new UnauthorizedAccessException($"Role '{role}' cannot {action} {resource}.");
    }
}

public sealed class QuotationService(IDbContextFactory<LedgerNestDbContext> factory)
{
    public async Task CancelAsync(int quotationId, string reason, string user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Cancellation reason is required.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var quotation = await db.Invoices.SingleAsync(item => item.Id == quotationId && item.Type == "Quotation" && item.DeletedAt == null, cancellationToken);
        if (!QuotationRules.CanCancel(quotation.Status)) throw new InvalidOperationException($"A {quotation.Status.ToLowerInvariant()} quotation cannot be cancelled.");
        quotation.Status = "Cancelled";
        quotation.CancellationReason = reason.Trim();
        quotation.CancelledBy = user;
        quotation.CancelledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
