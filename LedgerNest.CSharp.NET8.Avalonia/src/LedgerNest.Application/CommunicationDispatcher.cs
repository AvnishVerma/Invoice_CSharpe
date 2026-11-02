using System.Globalization;
using System.Text.RegularExpressions;
using LedgerNest.Domain;

namespace LedgerNest.Application;

public static partial class NotificationTemplates
{
    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Placeholder();
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        if (template.Length > 16000) throw new InvalidOperationException("Message template is too long.");
        var literal = Placeholder().Replace(template, "");
        if (literal.Contains('{') || literal.Contains('}')) throw new InvalidOperationException("Use only the documented placeholder names in braces.");
        return Placeholder().Replace(template, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? value : throw new InvalidOperationException("Unknown placeholder: " + match.Value));
    }
}

public sealed class CommunicationDispatcher(ICommunicationStore store, IEnumerable<INotificationProvider> providers, TimeProvider? clock = null)
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    public static readonly string[] Events = ["New invoice", "Invoice finalized", "Invoice voided", "Payment received", "Invoice overdue", "Delivery successful", "Delivery failed", "Connection failure", "Daily report", "Weekly report"];

    public int QueueInvoice(Invoice invoice, Customer customer, byte[] pdf, string company, string currency,
        IEnumerable<DeliveryChannel>? requested = null, string? resendId = null)
    {
        if (invoice.Id <= 0 || invoice.Type != "Invoice" || invoice.DeletedAt != null || invoice.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) || InvoiceStatusRules.IsVoided(invoice.Status))
            throw new InvalidOperationException("Only persisted, finalized invoices can be sent.");
        if (invoice.CustomerId != customer.Id) throw new InvalidOperationException("Delivery customer must match the saved invoice.");
        if (pdf.Length < 5 || pdf.Length > 15 * 1024 * 1024 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidOperationException("A valid invoice PDF under 15 MB is required.");
        var config = store.LoadConfiguration();
        var consent = store.Consent(customer.Id);
        var values = new Dictionary<string, string>
        {
            ["InvoiceNumber"] = invoice.InvoiceNumber, ["CompanyName"] = company, ["CustomerName"] = customer.Name,
            ["InvoiceDate"] = invoice.InvoiceDate.ToString("yyyy-MM-dd"), ["Currency"] = currency,
            ["TotalAmount"] = invoice.GrandTotal.ToString("0.00", CultureInfo.InvariantCulture),
            ["PaymentStatus"] = invoice.Status, ["Footer"] = config.Footer
        };
        var count = 0;
        var channels = (requested ?? Enum.GetValues<DeliveryChannel>()).Distinct().ToArray();
        foreach (var channel in channels)
        {
            if (!config.For(channel).Enabled || !consent.Allows(channel)) continue;
            var recipient = channel switch { DeliveryChannel.Email => customer.Email ?? "", DeliveryChannel.WhatsApp => customer.Phone ?? "", _ => consent.TelegramChatId };
            var bodyTemplate = channel == DeliveryChannel.Telegram ? config.TelegramBody ?? config.Body : config.Body;
            var message = new DeliveryMessage { Subject = NotificationTemplates.Render(config.Subject, values), Body = NotificationTemplates.Render(bodyTemplate, values),
                Pdf = pdf, TemplateParameters = [customer.Name, invoice.InvoiceNumber, company, currency + " " + values["TotalAmount"], values["InvoiceDate"]] };
            var job = new DeliveryJob { InvoiceId = invoice.Id, InvoiceNumber = invoice.InvoiceNumber, CustomerId = customer.Id,
                Recipient = recipient, Channel = channel, Message = message, CreatedAt = time.GetUtcNow(),
                DeduplicationKey = $"invoice:{invoice.Id}:{channel}:{resendId ?? "initial"}" };
            try { job.Recipient = ValidateRecipient(channel, recipient, config.For(channel).CountryCode); }
            catch (InvalidOperationException error)
            {
                job.State = DeliveryState.Failed;
                job.Error = "Contact validation failed: " + error.Message + " Update the customer contact and use Resend Invoice.";
                if (store.Enqueue(job)) QueueAdmin("Delivery failed", job.Id, $"Invoice {invoice.InvoiceNumber}: invalid {channel} contact.", invoice.Id);
                if (channels.Length == 1) throw;
                continue;
            }
            if (store.Enqueue(job)) count++;
        }
        return count;
    }

    public int QueueAdmin(string eventName, string key, string body, int? invoiceId = null)
    {
        var config = store.LoadConfiguration(); var count = 0;
        foreach (var channel in config.Channels.Where(c => c.Enabled && c.Events.Contains(eventName) && c.AdminRecipient.Length > 0))
        {
            var recipient = ValidateRecipient(channel.Channel, channel.AdminRecipient, channel.CountryCode);
            if (store.Enqueue(new DeliveryJob { Channel = channel.Channel, Recipient = recipient, InvoiceId = invoiceId,
                NotificationType = eventName, DeduplicationKey = $"admin:{eventName}:{key}:{channel.Channel}",
                CreatedAt = time.GetUtcNow(), Message = new DeliveryMessage { Subject = eventName, Body = body,
                    TemplateParameters = [eventName, body] } })) count++;
        }
        return count;
    }

    public async Task ProcessAsync(CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) return;
        try
        {
            var now = time.GetUtcNow();
            var coolingDown = store.History().Where(j => j.State is DeliveryState.RetryScheduled or DeliveryState.Failed && j.NextAttemptAt > now)
                .Select(j => j.Channel).ToHashSet();
            foreach (var job in store.History().Where(j => j.State == DeliveryState.Processing && j.LastAttemptAt < now.AddMinutes(-5)))
            {
                job.State = DeliveryState.Failed; job.Error = "Interrupted send; delivery is uncertain. Verify with the provider before retrying."; store.Save(job);
            }
            var enabled = store.LoadConfiguration().Channels.Where(c => c.Enabled).Select(c => c.Channel).ToHashSet();
            foreach (var job in store.History()
                .Where(j => j.State is DeliveryState.Pending or DeliveryState.RetryScheduled && (j.NextAttemptAt == null || j.NextAttemptAt <= now))
                .OrderBy(j => j.CreatedAt)
                .Where(j => enabled.Contains(j.Channel) && !coolingDown.Contains(j.Channel)).Take(20))
            {
                token.ThrowIfCancellationRequested();
                var config = store.LoadConfiguration();
                if (!config.For(job.Channel).Enabled || coolingDown.Contains(job.Channel)) continue;
                if (Events.Contains(job.NotificationType))
                {
                    var channel = config.For(job.Channel);
                    string destination;
                    try { destination = ValidateRecipient(job.Channel, channel.AdminRecipient, channel.CountryCode); }
                    catch (InvalidOperationException) { destination = ""; }
                    if (!channel.Events.Contains(job.NotificationType) || destination != job.Recipient)
                    {
                        job.State = DeliveryState.Cancelled;
                        job.Error = "Administrator notification preference or destination changed.";
                        store.Save(job); continue;
                    }
                }
                if (job.NotificationType == "Invoice" && job.InvoiceId is int invoiceId)
                {
                    if (!store.IsInvoiceSendable(invoiceId) || job.CustomerId is not int customerId || !store.Consent(customerId).Allows(job.Channel))
                    { job.State = DeliveryState.Cancelled; job.Error = "Invoice unavailable or customer consent withdrawn."; store.Save(job); continue; }
                }
                if (!store.TryClaim(job)) continue;
                if (job.NotificationType == "Invoice" && (job.Message.Pdf is not { Length: > 5 } pdf || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8)))
                { job.State = DeliveryState.Failed; job.Error = "Invoice PDF is missing. Regenerate delivery from the saved invoice."; store.Save(job); continue; }
                NotificationResult result;
                try { result = await providers.Single(p => p.Channel == job.Channel).SendAsync(config.For(job.Channel), job, token); }
                catch (OperationCanceledException) { result = new(false, Error: "Send interrupted; verify provider history before retrying."); }
                catch { result = new(false, Error: "Provider request failed; verify configuration and provider history."); }
                job.RetryCount++; job.LastAttemptAt = time.GetUtcNow(); job.Error = result.Error;
                if (result.Success)
                { job.State = DeliveryState.Sent; job.SentAt = time.GetUtcNow(); job.ProviderMessageId = result.MessageId; job.NextAttemptAt = null; }
                else if (result.RetrySafe && job.RetryCount <= Math.Clamp(config.RetryLimit, 0, 10))
                { job.State = DeliveryState.RetryScheduled; job.NextAttemptAt = time.GetUtcNow() + (result.RetryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, job.RetryCount) * 30)); coolingDown.Add(job.Channel); }
                else job.State = DeliveryState.Failed;
                if (!result.Success && result.RetrySafe && result.RetryAfter is { } pause)
                {
                    job.NextAttemptAt = time.GetUtcNow() + pause;
                    coolingDown.Add(job.Channel);
                }
                store.Save(job);
                if (job.NotificationType == "Invoice")
                {
                    QueueAdmin(result.Success ? "Delivery successful" : "Delivery failed", job.Id, $"Invoice {job.InvoiceNumber}: {job.State}.", job.InvoiceId);
                    if (!result.Success) QueueAdmin("Connection failure", job.Id, $"Communication request failed for {job.Channel}. Review notification history.");
                }
            }
        }
        finally { gate.Release(); }
    }

    public static string ValidateRecipient(DeliveryChannel channel, string recipient, string country = "91")
    {
        recipient = recipient.Trim();
        if (channel == DeliveryChannel.Email)
        {
            if (!System.Net.Mail.MailAddress.TryCreate(recipient, out var address) || address.Address != recipient || recipient.Contains('\r') || recipient.Contains('\n'))
                throw new InvalidOperationException("Enter one valid email address.");
        }
        else if (channel == DeliveryChannel.WhatsApp)
        {
            var international = recipient.StartsWith('+');
            recipient = new string(recipient.Where(char.IsAsciiDigit).ToArray());
            if (!international && recipient.Length == 10) recipient = country.TrimStart('+') + recipient;
            if (recipient.Length is < 8 or > 15) throw new InvalidOperationException("Enter an international phone number.");
        }
        else if (!Regex.IsMatch(recipient, @"^-?\d+$|^@[A-Za-z][A-Za-z0-9_]{4,}$"))
            throw new InvalidOperationException("Enter a valid Telegram Chat ID.");
        return recipient;
    }
}
