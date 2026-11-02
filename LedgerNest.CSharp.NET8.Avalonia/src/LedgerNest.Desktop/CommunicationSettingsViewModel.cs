using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Application;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public sealed class CommunicationChannelEditor : ObservableObject
{
    public ChannelConfiguration Configuration { get; }
    public string Title => Configuration.Channel.ToString();
    public FormFieldsViewModel Fields { get; }
    private readonly Dictionary<PropertyInfo, FormField> fields = [];
    private readonly Dictionary<string, FormField> events = [];
    public FormFieldsViewModel Events { get; }
    public bool ConfirmDisable { get; set; }
    public bool ResetRequested { get; private set; }
    public IRelayCommand ResetCommand { get; }
    public CommunicationChannelEditor(ChannelConfiguration configuration)
    {
        Configuration = configuration;
        var shared = new[] { "Enabled", "TimeoutSeconds", "AdminRecipient", "TestRecipient" };
        var names = shared.Concat(configuration.Channel switch
        {
            DeliveryChannel.Email => new[] { "Host", "Port", "Security", "Authenticate", "Username", "Secret", "Sender", "SenderName", "ReplyTo" },
            DeliveryChannel.WhatsApp => new[] { "Provider", "BusinessAccountId", "PhoneNumberId", "Secret", "ApiVersion", "CountryCode", "TemplateName", "AdminTemplateName", "TemplateLanguage", "SendPdf" },
            _ => new[] { "Secret", "BotUsername", "MessageFormat", "SendPdf" }
        });
        foreach (var name in names)
        {
            var property = typeof(ChannelConfiguration).GetProperty(name)!;
            var label = Regex.Replace(name, "(?<!^)([A-Z])", " $1");
            label = name switch
            {
                "Enabled" => "Enable " + Title + " Integration",
                "Host" => "SMTP Server", "Port" => "SMTP Port", "Security" => "SMTP Security",
                "AdminRecipient" => configuration.Channel == DeliveryChannel.Email ? "Admin Email Address" :
                    configuration.Channel == DeliveryChannel.WhatsApp ? "Admin WhatsApp Number" : "Admin Telegram Chat ID",
                "TestRecipient" => configuration.Channel == DeliveryChannel.Email ? "Test Email Address" :
                    configuration.Channel == DeliveryChannel.WhatsApp ? "Test Recipient Number" : "Test Chat ID",
                _ => label
            };
            if (name == "Secret") label = configuration.Channel == DeliveryChannel.Email ? "SMTP Password / App Password" : configuration.Channel == DeliveryChannel.WhatsApp ? "Access Token" : "Bot Token";
            var kind = property.PropertyType == typeof(bool) ? "toggle" : property.PropertyType == typeof(int) ? "number" : name == "Secret" ? "password" : "text";
            string[]? options = name switch { "Security" => ["STARTTLS", "None"], "Provider" => ["Meta Cloud API"], "MessageFormat" => ["Plain text", "HTML"], _ => null };
            if (options != null) kind = "choice";
            var value = name == "Secret" ? "" : property.GetValue(configuration)?.ToString() ?? "";
            if (property.PropertyType == typeof(bool)) value = value.ToLowerInvariant();
            fields[property] = new FormField(label, value, kind, options)
            {
                IsReadOnly = name == "BotUsername",
                Help = name == "Secret" ? "Leave blank to keep the saved credential. Saved credentials are never displayed." :
                    name == "TemplateName" ? "Approved Meta template: invoice body parameters are customer, invoice number, company, amount, date. Admin templates use event, report text. Configure the approved template accordingly." :
                    name == "ApiVersion" ? "Enter a currently supported Meta Graph API version, for example vNN.0." :
                    name == "AdminRecipient" ? "Use only an administrator destination that has consented to these notifications." : ""
            };
        }
        Fields = new(fields.Values.Select(f => new FormFieldViewModel(f)).ToArray(), 2);
        foreach (var name in CommunicationDispatcher.Events) events[name] = new FormField(name, configuration.Events.Contains(name) ? "true" : "false", "toggle");
        Events = new(events.Values.Select(f => new FormFieldViewModel(f)).ToArray(), 2);
        ResetCommand = new RelayCommand(() =>
        {
            ResetRequested = true;
            var empty = new ChannelConfiguration { Channel = configuration.Channel };
            foreach (var (property, field) in fields)
            {
                field.Value = property.Name == "Secret" ? "" : property.GetValue(empty)?.ToString() ?? "";
                if (property.PropertyType == typeof(bool)) field.IsChecked = (bool)property.GetValue(empty)!;
            }
            foreach (var field in events.Values) field.IsChecked = false;
        });
    }
    public ChannelConfiguration Read()
    {
        var result = JsonSerializer.Deserialize<ChannelConfiguration>(JsonSerializer.Serialize(Configuration))!;
        foreach (var (property, field) in fields)
        {
            if (!field.Validate()) throw new InvalidOperationException(field.Error);
            if (property.Name == "Secret" && field.Value.Length == 0 && !ResetRequested) continue;
            object value = property.PropertyType == typeof(bool) ? field.IsChecked :
                property.PropertyType == typeof(int) ? int.Parse(field.Value) : property.Name == "Secret" ? field.Value : field.Value.Trim();
            property.SetValue(result, value);
        }
        result.Events = events.Where(e => e.Value.IsChecked).Select(e => e.Key).ToList();
        if (result.TimeoutSeconds is < 5 or > 120) throw new InvalidOperationException("Timeout must be between 5 and 120 seconds.");
        return result;
    }
    public void SetBotUsername(string username)
    {
        Configuration.BotUsername = username;
        fields.Single(f => f.Key.Name == "BotUsername").Value.Value = username;
    }
}

public sealed partial class CommunicationSettingsViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly MainWindowViewModel workspace;
    private CommunicationConfiguration configuration;
    public ObservableCollection<CommunicationChannelEditor> Channels { get; } = [];
    public ObservableCollection<DeliveryJob> History { get; } = [];
    public IReadOnlyList<UiRecord> Customers { get; }
    [ObservableProperty] private UiRecord? selectedCustomer;
    [ObservableProperty] private bool emailConsent;
    [ObservableProperty] private bool whatsAppConsent;
    [ObservableProperty] private bool telegramConsent;
    [ObservableProperty] private string telegramChatId = "";
    private bool telegramVerified;
    [ObservableProperty] private bool confirmPendingDisable;
    [ObservableProperty] private bool confirmResend;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string filter = "";
    [ObservableProperty] private string channelFilter = "All";
    [ObservableProperty] private DateTime? fromDate;
    [ObservableProperty] private DateTime? toDate;
    [ObservableProperty] private bool isBusy;
    public string[] ChannelFilters { get; } = ["All", "Email", "WhatsApp", "Telegram"];
    public IEnumerable<DeliveryJob> FilteredHistory => History.Where(j =>
        (ChannelFilter == "All" || j.Channel.ToString() == ChannelFilter) &&
        (!FromDate.HasValue || j.CreatedAt.LocalDateTime.Date >= FromDate.Value.Date) && (!ToDate.HasValue || j.CreatedAt.LocalDateTime.Date <= ToDate.Value.Date) &&
        (Filter.Length == 0 || $"{j.InvoiceNumber} {j.CustomerId} {j.Recipient} {j.State}".Contains(Filter, StringComparison.OrdinalIgnoreCase))).Take(500);
    public FormField Automatic { get; }
    public FormFieldsViewModel TemplateFields { get; }
    private readonly FormField subject, body, telegramBody, previewChannel, footer, retryLimit;
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<CommunicationChannelEditor> TestCommand { get; }
    public IAsyncRelayCommand<CommunicationChannelEditor> SendTestCommand { get; }
    public IRelayCommand SaveConsentCommand { get; }
    public IAsyncRelayCommand VerifyTelegramCommand { get; }
    public IRelayCommand<DeliveryJob> RetryCommand { get; }
    public IRelayCommand<DeliveryJob> ResendCommand { get; }
    public IRelayCommand PreviewCommand { get; }
    public string Preview { get; private set; } = "";
    public CommunicationSettingsViewModel(MainWindowViewModel workspace)
    {
        this.workspace = workspace;
        if (!workspace.CanConfigureCommunication) throw new UnauthorizedAccessException("Administrator settings access is required.");
        configuration = workspace.CommunicationStore.LoadConfiguration();
        foreach (var channel in configuration.Channels) Channels.Add(new(channel));
        Customers = workspace.Customers.Where(c => c.SourceId > 0).ToArray();
        Automatic = new("Automatically deliver finalized invoices to consented customers", configuration.AutomaticInvoices ? "true" : "false", "toggle");
        subject = new("Email Subject", configuration.Subject);
        body = new("Message Body (SMTP)", configuration.Body, "multiline") { MaxLength = 16000 };
        telegramBody = new("Telegram Message", configuration.TelegramBody ?? configuration.Body, "multiline") { MaxLength = 16000 };
        previewChannel = new("Preview Channel", "Email", "choice", ["Email", "Telegram"]);
        footer = new("Footer", configuration.Footer);
        retryLimit = new("Retry Limit", configuration.RetryLimit.ToString(), "number");
        TemplateFields = new(new[] { Automatic, subject, body, telegramBody, footer, retryLimit, previewChannel }.Select(f => new FormFieldViewModel(f)).ToArray(), 1);
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Refresh);
        TestCommand = new AsyncRelayCommand<CommunicationChannelEditor>(editor => Test(editor, false));
        SendTestCommand = new AsyncRelayCommand<CommunicationChannelEditor>(editor => Test(editor, true));
        SaveConsentCommand = new RelayCommand(SaveConsent);
        VerifyTelegramCommand = new AsyncRelayCommand(VerifyTelegram);
        RetryCommand = new RelayCommand<DeliveryJob>(job => Requeue(job, false));
        ResendCommand = new RelayCommand<DeliveryJob>(job => Requeue(job, true));
        PreviewCommand = new RelayCommand(() => Guard(() =>
        {
            var values = PreviewValues(); values["Footer"] = footer.Value;
            Preview = NotificationTemplates.Render(previewChannel.Value == "Telegram" ? telegramBody.Value : body.Value, values);
            OnPropertyChanged(nameof(Preview));
        }));
        Refresh();
    }
    private bool Authorized() => !cancellation.IsCancellationRequested && workspace.CanConfigureCommunication;
    public void Dispose() => cancellation.Cancel();
    private void Guard(Action action)
    {
        try { if (!Authorized()) throw new UnauthorizedAccessException("Administrator settings access is required."); action(); }
        catch (Exception e) when (e is InvalidOperationException or UnauthorizedAccessException or FormatException) { Status = e.Message; }
        catch { Status = "Communication settings unavailable. Check database and protected-storage access."; }
    }
    private static Dictionary<string, string> PreviewValues() => new() { ["InvoiceNumber"] = "PREVIEW", ["CompanyName"] = "Your company", ["CustomerName"] = "Customer",
        ["InvoiceDate"] = DateTime.Today.ToString("yyyy-MM-dd"), ["Currency"] = "INR", ["TotalAmount"] = "100.00", ["PaymentStatus"] = "Unpaid", ["Footer"] = "Thank you" };
    private void Save() => Guard(() =>
    {
        if (IsBusy) return;
        var next = new CommunicationConfiguration { Channels = Channels.Select(c => c.Read()).ToList(), AutomaticInvoices = Automatic.IsChecked,
            Subject = subject.Value, Body = body.Value, TelegramBody = telegramBody.Value, Footer = footer.Value, RetryLimit = int.Parse(retryLimit.Value) };
        NotificationTemplates.Render(next.Subject, PreviewValues()); NotificationTemplates.Render(next.Body, PreviewValues());
        NotificationTemplates.Render(next.TelegramBody!, PreviewValues());
        if (next.RetryLimit is < 0 or > 10) throw new InvalidOperationException("Retry limit must be 0 to 10.");
        foreach (var channel in next.Channels.Where(c => c.Enabled))
        {
            if (channel.Channel == DeliveryChannel.Email && (channel.Host.Length == 0 || channel.Sender.Length == 0 || channel.Port is < 1 or > 65535))
                throw new InvalidOperationException("Email requires SMTP host, valid port and sender.");
            if (channel.Channel == DeliveryChannel.Email && channel.Security == "None" && channel.Authenticate)
                throw new InvalidOperationException("SMTP authentication requires STARTTLS.");
            if (channel.Channel == DeliveryChannel.Email)
            {
                CommunicationDispatcher.ValidateRecipient(DeliveryChannel.Email, channel.Sender);
                if (channel.ReplyTo.Length > 0) CommunicationDispatcher.ValidateRecipient(DeliveryChannel.Email, channel.ReplyTo);
                if (channel.Authenticate && (channel.Username.Length == 0 || channel.Secret.Length == 0))
                    throw new InvalidOperationException("SMTP authentication requires username and password.");
            }
            if (channel.Channel != DeliveryChannel.Email && channel.Secret.Length == 0)
                throw new InvalidOperationException("An enabled provider requires a credential.");
            if (channel.Channel == DeliveryChannel.WhatsApp && (channel.PhoneNumberId.Length == 0 || channel.TemplateName.Length == 0 || channel.ApiVersion.Length == 0))
                throw new InvalidOperationException("WhatsApp requires a phone number ID, approved template and API version.");
            if (channel.AdminRecipient.Length > 0) CommunicationDispatcher.ValidateRecipient(channel.Channel, channel.AdminRecipient, channel.CountryCode);
        }
        var pending = workspace.CommunicationStore.History().Where(j => j.State is DeliveryState.Pending or DeliveryState.Processing or DeliveryState.RetryScheduled).ToArray();
        if (!ConfirmPendingDisable && next.Channels.Any(c => !c.Enabled && configuration.For(c.Channel).Enabled && pending.Any(j => j.Channel == c.Channel)))
            throw new InvalidOperationException("Pending notifications exist. Confirm disabling pending integrations before saving; pending work will pause.");
        workspace.CommunicationStore.SaveConfiguration(next); configuration = next;
        // Rebuild editors so credentials are blank and subsequent saves retain the newly stored secret.
        Channels.Clear(); foreach (var channel in next.Channels) Channels.Add(new(channel));
        Status = "Communication configuration saved. Tests do not automatically enable integrations.";
    });
    private async Task Test(CommunicationChannelEditor? editor, bool send)
    {
        if (editor == null || IsBusy || !Authorized()) return;
        IsBusy = true;
        try
        {
            var config = editor.Read(); var provider = workspace.CommunicationProviders.Single(p => p.Channel == config.Channel);
            NotificationResult result;
            if (send)
            {
                var recipient = CommunicationDispatcher.ValidateRecipient(config.Channel, config.TestRecipient, config.CountryCode);
                result = await provider.SendAsync(config, new DeliveryJob { Recipient = recipient,
                    NotificationType = "Test message",
                    Message = new DeliveryMessage { Subject = "LedgerNest integration test", Body = "LedgerNest test notification.",
                        Pdf = SimplePdf.Create(["LedgerNest integration test - not an invoice"]),
                        TemplateParameters = ["Test", "PREVIEW", "LedgerNest", "0.00", DateTime.Today.ToString("yyyy-MM-dd")] } }, cancellation.Token);
            }
            else result = await provider.TestAsync(config, cancellation.Token);
            if (result.Success && config.Channel == DeliveryChannel.Telegram && !send)
                editor.SetBotUsername(result.MessageId);
            workspace.CommunicationStore.Enqueue(new DeliveryJob { Channel = config.Channel, Recipient = send ? config.TestRecipient : "",
                DeduplicationKey = "test:" + Guid.NewGuid().ToString("N"), NotificationType = send ? "Test message" : "Connection test",
                State = result.Success ? DeliveryState.Sent : DeliveryState.Failed, Error = result.Error, ProviderMessageId = send ? result.MessageId : "",
                LastAttemptAt = DateTimeOffset.UtcNow, SentAt = result.Success ? DateTimeOffset.UtcNow : null, RetryCount = 1 });
            Status = result.Success ? "Provider accepted the test. This does not confirm recipient delivery. " + (send ? "" : result.MessageId) : result.Error;
        }
        catch { Status = "Test failed. Check configuration, recipient, credentials and provider permissions."; }
        finally { IsBusy = false; }
    }
    private void Refresh() => Guard(() => { History.Clear(); foreach (var job in workspace.CommunicationStore.History()) History.Add(job); OnPropertyChanged(nameof(FilteredHistory)); });
    partial void OnFilterChanged(string value) => OnPropertyChanged(nameof(FilteredHistory));
    partial void OnChannelFilterChanged(string value) => OnPropertyChanged(nameof(FilteredHistory));
    partial void OnFromDateChanged(DateTime? value) => OnPropertyChanged(nameof(FilteredHistory));
    partial void OnToDateChanged(DateTime? value) => OnPropertyChanged(nameof(FilteredHistory));
    partial void OnTelegramChatIdChanged(string value) => telegramVerified = false;
    partial void OnSelectedCustomerChanged(UiRecord? value)
    {
        if (value == null || !Authorized()) return;
        var consent = workspace.CommunicationStore.Consent(value.SourceId);
        EmailConsent = consent.Email; WhatsAppConsent = consent.WhatsApp; TelegramConsent = consent.Telegram;
        TelegramChatId = consent.TelegramChatId; telegramVerified = consent.TelegramVerified;
    }
    private void SaveConsent() => Guard(() =>
    {
        if (SelectedCustomer == null) throw new InvalidOperationException("Select a customer.");
        workspace.CommunicationStore.SaveConsent(new() { CustomerId = SelectedCustomer.SourceId, Email = EmailConsent, WhatsApp = WhatsAppConsent,
            Telegram = TelegramConsent, TelegramChatId = TelegramChatId, TelegramVerified = telegramVerified,
            UpdatedAt = DateTimeOffset.UtcNow, RecordedBy = workspace.CurrentUsername ?? "" });
        Status = "Consent saved. Record opt-in only after the customer explicitly agrees.";
    });
    private async Task VerifyTelegram()
    {
        if (!Authorized() || IsBusy) return;
        IsBusy = true;
        try
        {
            var config = Channels.Single(c => c.Configuration.Channel == DeliveryChannel.Telegram).Read();
            config.TestRecipient = CommunicationDispatcher.ValidateRecipient(DeliveryChannel.Telegram, TelegramChatId);
            var result = await workspace.CommunicationProviders.Single(p => p.Channel == DeliveryChannel.Telegram).TestAsync(config, cancellation.Token);
            telegramVerified = result.Success; Status = result.Success ? "Telegram destination verified. Save consent to retain this verification." : result.Error;
        }
        catch { Status = "Telegram destination could not be verified."; }
        finally { IsBusy = false; }
    }
    private void Requeue(DeliveryJob? job, bool resend) => Guard(() =>
    {
        if (job == null) return;
        job = workspace.CommunicationStore.History().SingleOrDefault(j => j.Id == job.Id);
        if (job == null) return;
        if (job.NotificationType == "Connection test") { Status = "Repeat connection checks with Test Connection."; return; }
        if (job.NextAttemptAt > DateTimeOffset.UtcNow) { Status = "The provider requested a pause. Wait until the next allowed attempt."; return; }
        if (!ConfirmResend) throw new InvalidOperationException("Confirm that provider history was checked before retrying or resending.");
        if (resend && job.State == DeliveryState.Sent)
        { job.Message = workspace.CommunicationStore.ReadMessage(job); job = JsonSerializer.Deserialize<DeliveryJob>(JsonSerializer.Serialize(job))!; job.Id = Guid.NewGuid().ToString("N"); job.DeduplicationKey += ":resend:" + job.Id; job.CreatedAt = DateTimeOffset.UtcNow; job.State = DeliveryState.Pending; job.RetryCount = 0; job.Error = ""; job.SentAt = null; job.ProviderMessageId = ""; workspace.CommunicationStore.Enqueue(job); }
        else if (!resend && job.State == DeliveryState.Failed)
        { job.State = DeliveryState.Pending; job.NextAttemptAt = null; workspace.CommunicationStore.Save(job); }
        Refresh();
    });
}
