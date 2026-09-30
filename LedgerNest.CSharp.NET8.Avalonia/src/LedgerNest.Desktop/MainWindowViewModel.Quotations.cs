using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public async Task<bool> CancelQuotationAsync(UiRecord quotation, string reason)
    {
        if (!HasPermission("Quotation", "Cancel")) { Status = "Your role cannot cancel quotations."; return false; }
        if (dbFactory == null || quotation.SourceId <= 0) { Status = "Quotation storage is unavailable."; return false; }
        try
        {
            await new QuotationService(dbFactory).CancelAsync(quotation.SourceId, reason, CurrentUsername ?? "system");
            quotation.Values["Status"] = "Cancelled";
            quotation.Values["Cancellation Reason"] = reason.Trim();
            Status = $"Quotation {quotation.Name} cancelled.";
            return true;
        }
        catch (Exception exception) { Status = exception.Message; return false; }
    }
}
