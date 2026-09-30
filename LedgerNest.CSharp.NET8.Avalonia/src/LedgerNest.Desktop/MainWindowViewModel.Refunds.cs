namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public InvoiceRefundViewModel? CreateRefundViewModel(UiRecord invoice, Action completed, Action close)
    {
        if (!HasPermission("Invoice", "Refund")) { Status = "Your role cannot create refunds."; return null; }
        if (dbFactory == null) { Status = "Refund storage is unavailable."; return null; }
        return new InvoiceRefundViewModel(dbFactory, invoice, () => CurrentUsername ?? "system", completed, close);
    }
}
