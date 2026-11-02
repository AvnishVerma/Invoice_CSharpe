namespace LedgerNest.Domain;

public enum DeliveryChannel { Email, WhatsApp, Telegram }
public enum DeliveryState { Pending, Processing, Sent, Failed, RetryScheduled, Cancelled }

public sealed class CommunicationConfiguration
{
    public List<ChannelConfiguration> Channels { get; set; } = Enum.GetValues<DeliveryChannel>().Select(c => new ChannelConfiguration { Channel = c }).ToList();
    public int RetryLimit { get; set; } = 3;
    public bool AutomaticInvoices { get; set; }
    public string Subject { get; set; } = "Invoice #{InvoiceNumber} from {CompanyName}";
    public string Body { get; set; } = "Dear {CustomerName},\nYour invoice {InvoiceNumber} from {CompanyName} is ready.\nDate: {InvoiceDate}\nAmount: {Currency} {TotalAmount}\nStatus: {PaymentStatus}\n{Footer}";
    public string? TelegramBody { get; set; }
    public string Footer { get; set; } = "Thank you for your business.";
    public ChannelConfiguration For(DeliveryChannel channel) => Channels.Single(c => c.Channel == channel);
}

public sealed class ChannelConfiguration
{
    public DeliveryChannel Channel { get; set; }
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Security { get; set; } = "STARTTLS";
    public bool Authenticate { get; set; } = true;
    public string Username { get; set; } = "";
    public string Secret { get; set; } = "";
    public string Sender { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string ReplyTo { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public string Provider { get; set; } = "Meta Cloud API";
    public string ApiVersion { get; set; } = "";
    public string BusinessAccountId { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string CountryCode { get; set; } = "91";
    public string TemplateName { get; set; } = "";
    public string AdminTemplateName { get; set; } = "";
    public string TemplateLanguage { get; set; } = "en";
    public bool SendPdf { get; set; } = true;
    public string BotUsername { get; set; } = "";
    public string MessageFormat { get; set; } = "Plain text";
    public string AdminRecipient { get; set; } = "";
    public string TestRecipient { get; set; } = "";
    public List<string> Events { get; set; } = [];
}

public sealed class CustomerCommunicationConsent
{
    public int CustomerId { get; set; }
    public bool Email { get; set; }
    public bool WhatsApp { get; set; }
    public bool Telegram { get; set; }
    public string TelegramChatId { get; set; } = "";
    public bool TelegramVerified { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string RecordedBy { get; set; } = "";
    public bool Allows(DeliveryChannel channel) => channel switch
    {
        DeliveryChannel.Email => Email, DeliveryChannel.WhatsApp => WhatsApp,
        _ => Telegram && TelegramVerified && TelegramChatId.Length > 0
    };
}

public sealed class DeliveryMessage
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public byte[]? Pdf { get; set; }
    public string FileName { get; set; } = "invoice.pdf";
    // Parameters must match the approved Meta template body, in this order.
    public string[] TemplateParameters { get; set; } = [];
}

public sealed class DeliveryJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeduplicationKey { get; set; } = "";
    public int? InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public int? CustomerId { get; set; }
    public string Recipient { get; set; } = "";
    public DeliveryChannel Channel { get; set; }
    public string NotificationType { get; set; } = "Invoice";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DeliveryState State { get; set; }
    public string ProviderMessageId { get; set; } = "";
    public int RetryCount { get; set; }
    public string Error { get; set; } = "";
    public DeliveryMessage Message { get; set; } = new();
}

public sealed record NotificationResult(bool Success, string MessageId = "", string Error = "", bool RetrySafe = false, TimeSpan? RetryAfter = null);
public interface INotificationProvider
{
    DeliveryChannel Channel { get; }
    Task<NotificationResult> SendAsync(ChannelConfiguration configuration, DeliveryJob request, CancellationToken cancellationToken);
    Task<NotificationResult> TestAsync(ChannelConfiguration configuration, CancellationToken cancellationToken);
}

public interface ICommunicationStore
{
    CommunicationConfiguration LoadConfiguration();
    void SaveConfiguration(CommunicationConfiguration configuration);
    IReadOnlyList<DeliveryJob> History();
    bool Enqueue(DeliveryJob job);
    bool TryClaim(DeliveryJob job);
    void Save(DeliveryJob job);
    CustomerCommunicationConsent Consent(int customerId);
    void SaveConsent(CustomerCommunicationConsent consent);
    bool IsInvoiceSendable(int invoiceId);
}
