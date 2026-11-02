using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    private CommunicationStore? communicationStore;
    private CommunicationDispatcher? communicationDispatcher;
    private IReadOnlyList<INotificationProvider>? communicationProviders;
    private readonly SemaphoreSlim communicationCycle = new(1);
    private readonly CancellationTokenSource communicationShutdown = new();
    private readonly object communicationLifetime = new();
    private CancellationTokenSource communicationSession = new();
    private DateTime lastOverdueScan;
    private void ResetCommunicationSession()
    {
        CancellationTokenSource previous;
        lock (communicationLifetime)
        {
            previous = communicationSession;
            communicationSession = new CancellationTokenSource();
        }
        previous.Cancel();
        previous.Dispose();
    }
    private CancellationTokenSource CommunicationCancellation()
    {
        lock (communicationLifetime)
            return CancellationTokenSource.CreateLinkedTokenSource(communicationShutdown.Token, communicationSession.Token);
    }
    internal bool CanConfigureCommunication => CurrentRole == "Admin" && HasPermission("Settings", "Update");
    internal CommunicationStore CommunicationStore => communicationStore ??= new(dbFactory ?? throw new InvalidOperationException("A saved company database is required."), new WindowsCommunicationProtection());
    internal IReadOnlyList<INotificationProvider> CommunicationProviders => communicationProviders ??=
    [
        new EmailNotificationProvider(),
        new CloudNotificationProvider(DeliveryChannel.WhatsApp, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })),
        new CloudNotificationProvider(DeliveryChannel.Telegram, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
    ];
    internal CommunicationDispatcher Communication => communicationDispatcher ??= new(CommunicationStore, CommunicationProviders);
    public void StopCommunication() => communicationShutdown.Cancel();

    // Runs away from the UI thread; no form values or invoice collections are accessed here.
    public async Task RunCommunicationAsync()
    {
        if (dbFactory == null || !CanAccessWorkspace || !await communicationCycle.WaitAsync(0)) return;
        try
        {
            using var cancellation = CommunicationCancellation();
            var token = cancellation.Token;
            await Task.Run(async () =>
            {
                var config = CommunicationStore.LoadConfiguration();
                if (!config.Channels.Any(c => c.Enabled)) return;
                var today = DateTime.Today;
                if (lastOverdueScan != today && config.Channels.Any(c => c.Enabled && c.Events.Contains("Invoice overdue")))
                {
                    using var db = dbFactory.CreateDbContext();
                    foreach (var invoice in db.Invoices.AsNoTracking().Where(InvoiceStatusRules.FinancialInvoices).ToArray()
                        .Where(i => !i.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) &&
                            i.Snapshot?.DueDate is { } due && due.Date < today && InvoiceStatusRules.Outstanding(i) > 0))
                    {
                        Communication.QueueAdmin("Invoice overdue", $"{invoice.Id}:{today:yyyy-MM-dd}",
                            $"Invoice {invoice.InvoiceNumber}\nDue: {invoice.Snapshot!.DueDate:yyyy-MM-dd}\nOutstanding: {InvoiceStatusRules.Outstanding(invoice):0.00} {invoice.Snapshot.Currency}", invoice.Id);
                    }
                    lastOverdueScan = today;
                }
                if (config.Channels.Any(c => c.Enabled && c.Events.Contains("Daily report")))
                    Communication.QueueAdmin("Daily report", today.ToString("yyyy-MM-dd"), CommunicationStore.BuildReport(today.AddDays(-1), today));
                var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                if (config.Channels.Any(c => c.Enabled && c.Events.Contains("Weekly report")))
                    Communication.QueueAdmin("Weekly report", monday.ToString("yyyy-MM-dd"), CommunicationStore.BuildReport(monday.AddDays(-7), monday));
                await Communication.ProcessAsync(token);
            }, token);
        }
        catch (OperationCanceledException) { }
        catch { Status = "Communication processing needs attention. Review Communication settings and protected-storage access."; }
        finally { communicationCycle.Release(); }
    }

    private void RecordCommunicationEvent(string eventName, int invoiceId, string key, decimal? paymentReceived = null)
    {
        if (dbFactory == null) return;
        try
        {
            using var db = dbFactory.CreateDbContext();
            var invoice = db.Invoices.AsNoTracking().Single(i => i.Id == invoiceId);
            if (invoice.Type != "Invoice") return;
            var currency = invoice.Snapshot?.Currency ?? "Unspecified";
            var payment = paymentReceived is { } received ? $"Payment received: {received:0.00} {currency}\n" : "";
            Communication.QueueAdmin(eventName, key, $"Invoice {invoice.InvoiceNumber}\n{payment}Invoice total: {invoice.GrandTotal:0.00} {currency}\nStatus: {invoice.Status}", invoice.Id);
        }
        catch { Status = "Business record saved; communication could not be queued. Review Communication settings."; }
    }

    internal int QueueCustomerDelivery(UiRecord record, IEnumerable<DeliveryChannel>? channels = null, bool resend = false)
    {
        if (!HasPermission("Invoice", "View") || !HasPermission("Invoice", "Update"))
            throw new UnauthorizedAccessException("Invoice delivery requires invoice view and update permission.");
        if (dbFactory == null) throw new InvalidOperationException("Save the invoice first.");
        using var db = dbFactory.CreateDbContext();
        var invoice = db.Invoices.AsNoTracking().Single(i => i.Id == record.SourceId);
        if (invoice.CustomerId is not int customerId) throw new InvalidOperationException("Select a saved customer and record delivery consent in Communication settings.");
        var customer = db.Customers.AsNoTracking().Single(c => c.Id == customerId);
        var context = BuildDocumentExportContext(record);
        var pdf = DocumentPdf.Create(context.Invoice, context.Items, context.Business, context.PageSize, context.Landscape, context.Template, context.ThemeColor, context.Options);
        var company = Settings["Company Info"].SelectMany(s => s.Fields).First(f => f.Label == "Company Name").Value;
        return Communication.QueueInvoice(invoice, customer, pdf, company, EditorCurrency, channels, resend ? Guid.NewGuid().ToString("N") : null);
    }

    private void QueueAutomaticDelivery(UiRecord record)
    {
        if (dbFactory == null) return;
        try
        {
            var config = CommunicationStore.LoadConfiguration();
            if (config.AutomaticInvoices && config.Channels.Any(c => c.Enabled)) QueueCustomerDelivery(record);
        }
        catch { Status = "Invoice saved; automatic delivery was not queued. Check customer consent, contacts and PDF configuration."; }
    }
}
