using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public sealed partial class RefundLineViewModel : ObservableObject
{
    public int InvoiceItemId { get; init; }
    public string Description { get; init; } = "";
    public string Unit { get; init; } = "";
    public decimal SoldQuantity { get; init; }
    public decimal PreviouslyRefunded { get; init; }
    public decimal RemainingQuantity => SoldQuantity - PreviouslyRefunded;
    public decimal UnitPrice { get; init; }
    public decimal TaxRate { get; init; }
    public bool PriceIncludesTax { get; init; }
    [ObservableProperty] private decimal refundQuantity;
    public decimal RefundTotal => RefundQuantity <= 0 ? 0 : RefundRules.Calculate(RefundQuantity, UnitPrice, TaxRate, PriceIncludesTax).Total;
    partial void OnRefundQuantityChanged(decimal value) => OnPropertyChanged(nameof(RefundTotal));
}

public sealed partial class InvoiceRefundViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext> factory;
    private readonly UiRecord invoice;
    private readonly Func<string> currentUser;
    private readonly Action completed;
    private readonly Action close;
    public ObservableCollection<RefundLineViewModel> Lines { get; } = [];
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;
    public string InvoiceNumber => invoice.Name;
    public string Customer => invoice["Customer"];
    public decimal RefundTotal => Lines.Sum(line => line.RefundTotal);

    public InvoiceRefundViewModel(IDbContextFactory<LedgerNestDbContext> factory, UiRecord invoice, Func<string> currentUser, Action completed, Action close)
    {
        this.factory = factory; this.invoice = invoice; this.currentUser = currentUser; this.completed = completed; this.close = close;
        using var db = factory.CreateDbContext();
        var refunded = db.InvoiceRefundLines.AsNoTracking()
            .Where(line => db.InvoiceRefunds.Where(refund => refund.InvoiceId == invoice.SourceId).Select(refund => refund.Id).Contains(line.InvoiceRefundId))
            .GroupBy(line => line.InvoiceItemId).Select(group => new { Id = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToDictionary(item => item.Id, item => item.Quantity);
        foreach (var item in db.InvoiceItems.AsNoTracking().Where(item => item.InvoiceId == invoice.SourceId).OrderBy(item => item.Id))
        {
            var row = new RefundLineViewModel { InvoiceItemId = item.Id, Description = item.Description, Unit = item.SellingUnitCode, SoldQuantity = item.Quantity,
                PreviouslyRefunded = refunded.GetValueOrDefault(item.Id), UnitPrice = item.UnitPrice, TaxRate = item.TaxRate, PriceIncludesTax = item.PriceIncludesTax };
            row.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(RefundLineViewModel.RefundTotal)) OnPropertyChanged(nameof(RefundTotal)); };
            Lines.Add(row);
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        Error = "";
        if (string.IsNullOrWhiteSpace(Reason)) { Error = "Refund reason is required."; return; }
        var selected = Lines.Where(line => line.RefundQuantity > 0).ToArray();
        if (selected.Length == 0) { Error = "Select at least one refund quantity."; return; }
        if (selected.Any(line => line.RefundQuantity > line.RemainingQuantity)) { Error = "A refund quantity exceeds the remaining sold quantity."; return; }
        IsBusy = true;
        try
        {
            var service = new InvoiceRefundService(factory, new NumberSeriesService(factory));
            var refund = await service.RefundAsync(invoice.SourceId, selected.Select(line => new RefundLineRequest(line.InvoiceItemId, line.RefundQuantity)).ToArray(), Reason, currentUser());
            Status = $"Refund {refund.RefundNumber} created.";
            completed(); close();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Close() => close();
}
