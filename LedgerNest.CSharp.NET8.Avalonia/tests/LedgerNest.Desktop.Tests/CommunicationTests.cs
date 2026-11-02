using System.Net;
using System.Text;
using System.Text.Json;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests;

public sealed class CommunicationTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nTest invoice\n%%EOF");
    private sealed class TestProtection : ICommunicationProtection
    {
        public string Protect(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Unprotect(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Provider(DeliveryChannel channel, NotificationResult? result = null) : INotificationProvider
    {
        public DeliveryChannel Channel => channel;
        public int Calls;
        public Task<NotificationResult> SendAsync(ChannelConfiguration c, DeliveryJob j, CancellationToken token)
        {
            if (j.NotificationType == "Invoice") Assert.NotNull(j.Message.Pdf);
            Calls++; return Task.FromResult(result ?? new NotificationResult(true, "provider-123"));
        }
        public Task<NotificationResult> TestAsync(ChannelConfiguration c, CancellationToken token) => Task.FromResult(new NotificationResult(true));
    }
    private sealed class BlockingProvider : INotificationProvider
    {
        public DeliveryChannel Channel => DeliveryChannel.Email;
        public int Calls;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<NotificationResult> SendAsync(ChannelConfiguration c, DeliveryJob j, CancellationToken token)
        {
            Calls++; Entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new(true);
        }
        public Task<NotificationResult> TestAsync(ChannelConfiguration c, CancellationToken token) => Task.FromResult(new NotificationResult(true));
    }
    [Fact]
    public async Task LogoutCancelsInFlightDeliveryAndPreventsFurtherSends()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var provider = new BlockingProvider();
        var workspace = new MainWindowViewModel(fixture, fixture.DatabasePath, new TestLicenseService(),
            communicationStore: store, communicationProviders: [provider]);
        using (var db = fixture.CreateDbContext()) { db.Users.Single(u => u.Username == "admin").PasswordChanged = true; db.SaveChanges(); }
        Assert.True(workspace.SignIn("admin", "admin"));
        var dispatcher = new CommunicationDispatcher(store, [provider]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email], "second");
        var work = workspace.RunCommunicationAsync();
        await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        workspace.MarkInvoiceFormClean(); workspace.SignOut();
        await work.WaitAsync(TimeSpan.FromSeconds(10));
        await workspace.RunCommunicationAsync();
        Assert.Equal(1, provider.Calls);
        Assert.Contains(store.History(), j => j.State == DeliveryState.Pending);
        Assert.Contains(store.History(), j => j.State == DeliveryState.Failed);
    }
    private static (CommunicationStore Store, Invoice Invoice, Customer Customer) Seed(TestDatabaseFixture fixture, bool enabled = true)
    {
        using var db = fixture.CreateDbContext();
        var customer = new Customer { Name = "Test customer", Email = "customer@example.com", Phone = "+919876543210" };
        db.Customers.Add(customer); db.SaveChanges();
        var invoice = new Invoice { InvoiceNumber = "COMM-1", CustomerId = customer.Id, CustomerName = customer.Name, Status = "Unpaid", GrandTotal = 100 };
        db.Invoices.Add(invoice); db.SaveChanges();
        var store = new CommunicationStore(fixture, new TestProtection());
        var config = new CommunicationConfiguration();
        foreach (var c in config.Channels) c.Enabled = enabled;
        store.SaveConfiguration(config);
        store.SaveConsent(new() { CustomerId = customer.Id, Email = true, WhatsApp = true, Telegram = true, TelegramVerified = true, TelegramChatId = "12345" });
        return (store, invoice, customer);
    }

    [Theory]
    [InlineData(DeliveryChannel.Email)] [InlineData(DeliveryChannel.WhatsApp)] [InlineData(DeliveryChannel.Telegram)]
    public async Task EachChannelQueuesAndRecordsActualProviderResult(DeliveryChannel channel)
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var provider = new Provider(channel); var dispatcher = new CommunicationDispatcher(store, [provider]);
        Assert.Equal(1, dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [channel]));
        Assert.Equal(0, dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [channel]));
        await dispatcher.ProcessAsync(default);
        var job = Assert.Single(store.History()); Assert.Equal(DeliveryState.Sent, job.State);
        Assert.Equal("provider-123", job.ProviderMessageId); Assert.NotNull(job.SentAt);
        await dispatcher.ProcessAsync(default); Assert.Equal(1, provider.Calls);
    }
    [Fact]
    public async Task DisabledIntegrationsStayInactiveAcrossRestart()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture, false);
        var provider = new Provider(DeliveryChannel.Email); var dispatcher = new CommunicationDispatcher(store, [provider]);
        Assert.Equal(0, dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR"));
        Assert.All(new CommunicationStore(fixture, new TestProtection()).LoadConfiguration().Channels, c => Assert.False(c.Enabled));
        await dispatcher.ProcessAsync(default); Assert.Equal(0, provider.Calls);
    }
    [Fact]
    public async Task MultipleChannelsAndConcurrentWorkersDoNotDuplicate()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var providers = Enum.GetValues<DeliveryChannel>().Select(c => new Provider(c)).ToArray();
        var dispatcher = new CommunicationDispatcher(store, providers);
        Assert.Equal(3, dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR"));
        await Task.WhenAll(dispatcher.ProcessAsync(default), dispatcher.ProcessAsync(default),
            new CommunicationDispatcher(store, providers).ProcessAsync(default));
        Assert.All(providers, p => Assert.Equal(1, p.Calls)); Assert.Equal(3, store.History().Count);
    }
    [Theory]
    [InlineData("Draft")] [InlineData("Voided")] [InlineData("Declined")]
    public void DraftAndVoidedInvoicesCannotQueue(string status)
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture); invoice.Status = status;
        Assert.Throws<InvalidOperationException>(() => new CommunicationDispatcher(store, []).QueueInvoice(invoice, customer, Pdf, "Company", "INR"));
        Assert.Empty(store.History());
    }
    [Theory]
    [InlineData(DeliveryChannel.Email)] [InlineData(DeliveryChannel.WhatsApp)]
    public void MissingContactIsRejected(DeliveryChannel channel)
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture); customer.Email = ""; customer.Phone = "";
        Assert.Throws<InvalidOperationException>(() => new CommunicationDispatcher(store, []).QueueInvoice(invoice, customer, Pdf, "Company", "INR", [channel]));
    }
    [Fact]
    public async Task MissingEmailDoesNotBlockConsentedWhatsAppDelivery()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture); customer.Email = "";
        var provider = new Provider(DeliveryChannel.WhatsApp); var dispatcher = new CommunicationDispatcher(store, [provider]);
        Assert.Equal(1, dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email, DeliveryChannel.WhatsApp]));
        await dispatcher.ProcessAsync(default);
        Assert.Equal(1, provider.Calls);
        Assert.Contains(store.History(), j => j.Channel == DeliveryChannel.Email && j.State == DeliveryState.Failed);
        Assert.Contains(store.History(), j => j.Channel == DeliveryChannel.WhatsApp && j.State == DeliveryState.Sent);
    }
    [Fact]
    public void TelegramRequiresOptInAndVerifiedDestination()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        store.SaveConsent(new() { CustomerId = customer.Id, Telegram = true, TelegramChatId = "123" });
        Assert.Equal(0, new CommunicationDispatcher(store, []).QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Telegram]));
    }
    [Fact]
    public void PdfFailureDoesNotQueue()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        Assert.Throws<InvalidOperationException>(() => new CommunicationDispatcher(store, []).QueueInvoice(invoice, customer, [], "Company", "INR"));
        Assert.Empty(store.History());
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ConsentWithdrawalAndVoidCancelPendingInvoice(bool voidInvoice)
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var provider = new Provider(DeliveryChannel.Email); var dispatcher = new CommunicationDispatcher(store, [provider]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        if (voidInvoice) { using var db = fixture.CreateDbContext(); db.Invoices.Single(i => i.Id == invoice.Id).Status = "Voided"; db.SaveChanges(); }
        else store.SaveConsent(new() { CustomerId = customer.Id });
        await dispatcher.ProcessAsync(default); Assert.Equal(0, provider.Calls); Assert.Equal(DeliveryState.Cancelled, Assert.Single(store.History()).State);
    }
    [Fact]
    public async Task DisablingAfterQueuePausesWorkUntilEnabledAgain()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var provider = new Provider(DeliveryChannel.Email); var dispatcher = new CommunicationDispatcher(store, [provider]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        var config = store.LoadConfiguration(); config.For(DeliveryChannel.Email).Enabled = false; store.SaveConfiguration(config);
        await dispatcher.ProcessAsync(default); Assert.Equal(0, provider.Calls); Assert.Equal(DeliveryState.Pending, Assert.Single(store.History()).State);
        config.For(DeliveryChannel.Email).Enabled = true; store.SaveConfiguration(config);
        await dispatcher.ProcessAsync(default); Assert.Equal(1, provider.Calls);
    }
    [Fact]
    public async Task PausedChannelCannotStarveAnIndependentEnabledChannel()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var provider = new Provider(DeliveryChannel.Email); var dispatcher = new CommunicationDispatcher(store, [provider]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        for (var i = 0; i < 25; i++)
            dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Telegram], "queued-" + i);
        var config = store.LoadConfiguration(); config.For(DeliveryChannel.Telegram).Enabled = false; store.SaveConfiguration(config);
        await dispatcher.ProcessAsync(default);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(25, store.History().Count(j => j.State == DeliveryState.Pending));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ChangedAdminDestinationOrPreferenceCancelsPendingNotification(bool changeDestination)
    {
        using var fixture = new TestDatabaseFixture(); var (store, _, _) = Seed(fixture);
        var config = store.LoadConfiguration(); var channel = config.For(DeliveryChannel.Email);
        channel.AdminRecipient = "old-admin@example.com"; channel.Events.Add("Daily report"); store.SaveConfiguration(config);
        var provider = new Provider(DeliveryChannel.Email); var dispatcher = new CommunicationDispatcher(store, [provider]);
        dispatcher.QueueAdmin("Daily report", "day", "Business report");
        if (changeDestination) channel.AdminRecipient = "new-admin@example.com"; else channel.Events.Clear();
        store.SaveConfiguration(config);
        await dispatcher.ProcessAsync(default);
        Assert.Equal(0, provider.Calls); Assert.Equal(DeliveryState.Cancelled, Assert.Single(store.History()).State);
    }
    [Fact]
    public async Task RateLimitRetriesOnlyAfterActualDeadline()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture); var clock = new Clock();
        var provider = new Provider(DeliveryChannel.Email, new(false, Error: "Rate limited", RetrySafe: true, RetryAfter: TimeSpan.FromMinutes(1)));
        var dispatcher = new CommunicationDispatcher(store, [provider], clock);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        await dispatcher.ProcessAsync(default); Assert.Equal(DeliveryState.RetryScheduled, Assert.Single(store.History()).State);
        await dispatcher.ProcessAsync(default); Assert.Equal(1, provider.Calls);
        clock.Now += TimeSpan.FromMinutes(2); await dispatcher.ProcessAsync(default); Assert.Equal(2, provider.Calls);
    }
    [Fact]
    public async Task ProviderPauseStillAppliesWhenAutomaticRetryLimitIsZero()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var config = store.LoadConfiguration(); config.RetryLimit = 0; store.SaveConfiguration(config);
        var clock = new Clock();
        var provider = new Provider(DeliveryChannel.Email, new(false, Error: "Rate limited", RetrySafe: true, RetryAfter: TimeSpan.FromMinutes(1)));
        var dispatcher = new CommunicationDispatcher(store, [provider], clock);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email], "second");
        await dispatcher.ProcessAsync(default); await dispatcher.ProcessAsync(default);
        Assert.Equal(1, provider.Calls);
        Assert.Contains(store.History(), j => j.State == DeliveryState.Pending);
        clock.Now += TimeSpan.FromMinutes(2); await dispatcher.ProcessAsync(default); Assert.Equal(2, provider.Calls);
    }
    [Fact]
    public async Task UncertainAndInterruptedSendsRequireManualReview()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var provider = new Provider(DeliveryChannel.Email, new(false, Error: "Unknown outcome"));
        var dispatcher = new CommunicationDispatcher(store, [provider]);
        dispatcher.QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email]);
        await dispatcher.ProcessAsync(default); await dispatcher.ProcessAsync(default); Assert.Equal(1, provider.Calls);
        var job = Assert.Single(store.History()); job.State = DeliveryState.Processing; job.LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-6); store.Save(job);
        await dispatcher.ProcessAsync(default); Assert.Equal(DeliveryState.Failed, Assert.Single(store.History()).State);
        Assert.Contains("uncertain", Assert.Single(store.History()).Error);
    }
    [Theory]
    [InlineData("Daily report")] [InlineData("Weekly report")] [InlineData("Invoice voided")] [InlineData("Payment received")] [InlineData("Invoice overdue")]
    public void AdminPreferencesAndReportsUsePersistedData(string eventName)
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, _) = Seed(fixture);
        var config = store.LoadConfiguration(); var email = config.For(DeliveryChannel.Email); email.AdminRecipient = "admin@example.com"; email.Events.Add(eventName); store.SaveConfiguration(config);
        var dispatcher = new CommunicationDispatcher(store, []);
        var report = store.BuildReport(DateTime.Today, DateTime.Today.AddDays(1));
        Assert.Contains("Sales: 100.00", report); Assert.Contains("Invoices by invoice date: 1", report);
        Assert.Equal(1, dispatcher.QueueAdmin(eventName, "period-1", report, invoice.Id));
        Assert.Equal(0, dispatcher.QueueAdmin(eventName, "period-1", report, invoice.Id));
        Assert.Equal(eventName, Assert.Single(store.History()).NotificationType);
    }
    [Fact]
    public void CredentialsAreProtectedAtRestAndCanBeReopened()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new TestDatabaseFixture(); var store = new CommunicationStore(fixture, new WindowsCommunicationProtection());
        var config = new CommunicationConfiguration(); config.For(DeliveryChannel.Email).Secret = "sensitive-app-password";
        store.SaveConfiguration(config);
        using var db = fixture.CreateDbContext();
        Assert.DoesNotContain("sensitive-app-password", db.Settings.Single(s => s.Key == "communication:v1:configuration").Value);
        Assert.Equal("sensitive-app-password", new CommunicationStore(fixture, new WindowsCommunicationProtection()).LoadConfiguration().For(DeliveryChannel.Email).Secret);
    }
    [Fact]
    public void TemplateUnknownPlaceholdersFailBeforeSending()
    {
        Assert.Throws<InvalidOperationException>(() => NotificationTemplates.Render("Invoice {Unknown}", new Dictionary<string, string>()));
        Assert.Equal("Hello Customer", NotificationTemplates.Render("Hello {CustomerName}", new Dictionary<string, string> { ["CustomerName"] = "Customer" }));
    }
    [Fact]
    public void EmailAndTelegramTemplatesAreIndependent()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, customer) = Seed(fixture);
        var config = store.LoadConfiguration(); config.Body = "Email {InvoiceNumber}"; config.TelegramBody = "Telegram {InvoiceNumber}"; store.SaveConfiguration(config);
        new CommunicationDispatcher(store, []).QueueInvoice(invoice, customer, Pdf, "Company", "INR", [DeliveryChannel.Email, DeliveryChannel.Telegram]);
        Assert.Equal("Email COMM-1", store.ReadMessage(store.History().Single(j => j.Channel == DeliveryChannel.Email)).Body);
        Assert.Equal("Telegram COMM-1", store.ReadMessage(store.History().Single(j => j.Channel == DeliveryChannel.Telegram)).Body);
    }
    [Fact]
    public void PaymentEventUsesActualReceivedAmountRatherThanInvoiceTotal()
    {
        using var fixture = new TestDatabaseFixture(); var (store, invoice, _) = Seed(fixture);
        var config = store.LoadConfiguration(); config.For(DeliveryChannel.Email).AdminRecipient = "admin@example.com";
        config.For(DeliveryChannel.Email).Events.Add("Payment received"); store.SaveConfiguration(config);
        var workspace = new MainWindowViewModel(fixture, fixture.DatabasePath, new TestLicenseService(),
            communicationStore: store, communicationProviders: [new Provider(DeliveryChannel.Email)]);
        Assert.True(workspace.SignIn("admin", "admin"));
        FormField[] payment = [new("Amount", "25", "number"), new("Date", DateTime.Today.ToString("yyyy-MM-dd"), "date"),
            new("Method", "Cash"), new("Tax", "0", "number"), new("Reference")];
        Assert.True(workspace.ApplyPayment(workspace.Invoices.Single(i => i.SourceId == invoice.Id), payment), workspace.Status);
        var message = store.ReadMessage(Assert.Single(store.History()));
        Assert.Contains("Payment received: 25.00", message.Body);
        Assert.Contains("Invoice total: 100.00", message.Body);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request);
    }
    [Fact]
    public async Task SmtpAdapterSendsPdfToIsolatedLoopbackServer()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var data = new List<string>();
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(cancellation.Token);
            using var reader = new StreamReader(socket.GetStream());
            using var writer = new StreamWriter(socket.GetStream()) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost SMTP test");
            var inData = false;
            while (await reader.ReadLineAsync(cancellation.Token) is { } line)
            {
                if (inData)
                {
                    if (line == ".") { inData = false; await writer.WriteLineAsync("250 queued"); }
                    else data.Add(line);
                }
                else if (line.StartsWith("DATA")) { inData = true; await writer.WriteLineAsync("354 send data"); }
                else if (line.StartsWith("QUIT")) { await writer.WriteLineAsync("221 bye"); break; }
                else await writer.WriteLineAsync("250 localhost");
            }
        }, cancellation.Token);
        var result = await new EmailNotificationProvider().SendAsync(new() { Host = "127.0.0.1", Port = port, Security = "None",
            Authenticate = false, Sender = "sender@example.com" }, new() { Recipient = "recipient@example.com",
            Message = new() { Subject = "Invoice test", Body = "Test", Pdf = Pdf } }, cancellation.Token);
        await server;
        Assert.True(result.Success);
        Assert.Contains("application/pdf", string.Join("\n", data), StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData(DeliveryChannel.Telegram)] [InlineData(DeliveryChannel.WhatsApp)]
    public async Task RealHttpAdaptersUploadPdfAndParseProviderAcceptance(DeliveryChannel channel)
    {
        var calls = new List<string>();
        using var client = new HttpClient(new Handler(async request =>
        {
            calls.Add(request.RequestUri!.AbsolutePath);
            var content = request.Content == null ? "" : await request.Content.ReadAsStringAsync();
            if (channel == DeliveryChannel.Telegram) { Assert.Contains("application/pdf", content); Assert.Contains("chat_id", content); }
            if (request.RequestUri.AbsolutePath.EndsWith("/messages")) { Assert.Contains("template", content); Assert.Contains("media123", content); }
            var json = request.RequestUri.AbsolutePath.EndsWith("/media") ? "{\"id\":\"media123\"}" :
                channel == DeliveryChannel.Telegram ? "{\"ok\":true,\"result\":{\"message_id\":99}}" : "{\"messages\":[{\"id\":\"wamid123\"}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }));
        var provider = new CloudNotificationProvider(channel, client);
        var config = new ChannelConfiguration { Channel = channel, Secret = channel == DeliveryChannel.Telegram ? "123:abc" : "secret",
            ApiVersion = "v23.0", PhoneNumberId = "12345", TemplateName = "invoice_ready" };
        var result = await provider.SendAsync(config, new DeliveryJob { Recipient = "12345", Message = new() { Pdf = Pdf, Body = "Invoice", TemplateParameters = ["Customer"] } }, default);
        Assert.True(result.Success); Assert.NotEmpty(result.MessageId); Assert.Equal(channel == DeliveryChannel.Telegram ? 1 : 2, calls.Count);
    }
    [Theory]
    [InlineData(401, false)] [InlineData(429, true)] [InlineData(503, false)]
    public async Task ProviderErrorsAreSafeAndRateLimitIsRetryable(int code, bool retry)
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)
            { Content = new StringContent("{\"ok\":false,\"description\":\"token-sensitive secret\"}") })));
        var result = await new CloudNotificationProvider(DeliveryChannel.Telegram, client).SendAsync(new() { Secret = "123:secret" },
            new() { Recipient = "12345", Message = new() { Body = "Test" } }, default);
        Assert.False(result.Success); Assert.Equal(retry, result.RetrySafe); Assert.DoesNotContain("secret", result.Error);
    }
}
