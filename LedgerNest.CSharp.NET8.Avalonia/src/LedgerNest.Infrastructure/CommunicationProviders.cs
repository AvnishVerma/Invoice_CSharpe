using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using LedgerNest.Domain;

namespace LedgerNest.Infrastructure;

public sealed class EmailNotificationProvider : INotificationProvider
{
    public DeliveryChannel Channel => DeliveryChannel.Email;
    public async Task<NotificationResult> TestAsync(ChannelConfiguration c, CancellationToken token)
    {
        try
        {
            Validate(c);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(c.TimeoutSeconds, 5, 120)));
            using var socket = new TcpClient(); await socket.ConnectAsync(c.Host, c.Port, timeout.Token);
            using var reader = new StreamReader(socket.GetStream());
            var greeting = await reader.ReadLineAsync(timeout.Token);
            return greeting?.StartsWith("220") == true ? new(true, MessageId: "SMTP reachable; use Send Test to validate TLS and authentication.") : new(false, Error: "SMTP greeting rejected.");
        }
        catch { return new(false, Error: "SMTP connection failed. Check server, port and firewall."); }
    }
    public async Task<NotificationResult> SendAsync(ChannelConfiguration c, DeliveryJob request, CancellationToken token)
    {
        try
        {
            Validate(c);
            using var smtp = new SmtpClient(c.Host, c.Port) { EnableSsl = c.Security == "STARTTLS", UseDefaultCredentials = false,
                Timeout = Math.Clamp(c.TimeoutSeconds, 5, 120) * 1000 };
            if (c.Authenticate) smtp.Credentials = new NetworkCredential(c.Username, c.Secret);
            using var message = new MailMessage { From = new MailAddress(c.Sender, c.SenderName), Subject = request.Message.Subject.Replace("\r", "").Replace("\n", ""), Body = request.Message.Body };
            message.To.Add(request.Recipient);
            if (c.ReplyTo.Length > 0) message.ReplyToList.Add(new MailAddress(c.ReplyTo));
            if (request.Message.Pdf is { } pdf) message.Attachments.Add(new Attachment(new MemoryStream(pdf), request.Message.FileName, "application/pdf"));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(c.TimeoutSeconds, 5, 120)));
            await smtp.SendMailAsync(message, timeout.Token);
            return new(true); // SMTP acceptance is not confirmation of recipient delivery.
        }
        catch (SmtpException e)
        {
            var retry = e.StatusCode is SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.MailboxBusy;
            return new(false, Error: $"SMTP rejected the request ({(int)e.StatusCode}). Check authentication, sender permissions and recipient.", RetrySafe: retry);
        }
        catch { return new(false, Error: "SMTP send failed or timed out. Check configuration and provider history before retrying."); }
    }
    private static void Validate(ChannelConfiguration c)
    {
        if (string.IsNullOrWhiteSpace(c.Host) || c.Port is < 1 or > 65535 || !MailAddress.TryCreate(c.Sender, out _))
            throw new InvalidOperationException("Configure SMTP host, port and sender.");
        if (c.Security is not ("STARTTLS" or "None")) throw new InvalidOperationException("This SMTP adapter supports STARTTLS or None, not implicit SSL.");
        if (c.Security == "None" && c.Authenticate) throw new InvalidOperationException("Authentication requires STARTTLS.");
    }
}

public sealed class CloudNotificationProvider(DeliveryChannel channel, HttpClient client) : INotificationProvider, IDisposable
{
    public void Dispose() => client.Dispose();
    public DeliveryChannel Channel => channel;
    private static string Version(ChannelConfiguration c)
    {
        if (!Regex.IsMatch(c.ApiVersion, @"^v\d+\.\d+$") || !Regex.IsMatch(c.PhoneNumberId, @"^\d+$"))
            throw new InvalidOperationException("Configure a supported Graph API version and Phone Number ID.");
        return c.ApiVersion + "/" + c.PhoneNumberId;
    }
    private string Base(ChannelConfiguration c) => channel == DeliveryChannel.Telegram
        ? "https://api.telegram.org/bot" + (Regex.IsMatch(c.Secret, @"^\d+:[A-Za-z0-9_-]+$") ? c.Secret : throw new InvalidOperationException("Invalid bot token.")) + "/"
        : "https://graph.facebook.com/" + Version(c) + "/";
    private HttpRequestMessage Request(ChannelConfiguration c, string method, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(content == null ? HttpMethod.Get : HttpMethod.Post, Base(c) + method) { Content = content };
        if (channel == DeliveryChannel.WhatsApp) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.Secret);
        return request;
    }
    public async Task<NotificationResult> TestAsync(ChannelConfiguration c, CancellationToken token)
    {
        try
        {
            using var request = Request(c, channel == DeliveryChannel.Telegram ? "getMe" : "?fields=id,display_phone_number");
            var (result, json) = await Execute(request, c, token);
            if (result.Success && channel == DeliveryChannel.Telegram && json.TryGetProperty("result", out var bot) && bot.TryGetProperty("username", out var username))
            {
                if (c.TestRecipient.Length > 0)
                {
                    using var destination = Request(c, "getChat?chat_id=" + Uri.EscapeDataString(c.TestRecipient));
                    var checkedChat = await Execute(destination, c, token);
                    if (!checkedChat.Result.Success) return checkedChat.Result;
                }
                return result with { MessageId = username.GetString() ?? "" };
            }
            return result;
        }
        catch { return new(false, Error: "Provider connection failed. Check credentials, IDs and network access."); }
    }
    public async Task<NotificationResult> SendAsync(ChannelConfiguration c, DeliveryJob job, CancellationToken token)
    {
        try
        {
            if (channel == DeliveryChannel.Telegram)
            {
                var withPdf = c.SendPdf && job.Message.Pdf != null;
                var body = c.MessageFormat == "HTML"
                    ? "<b>" + WebUtility.HtmlEncode(job.Message.Subject) + "</b>\n" + WebUtility.HtmlEncode(job.Message.Body)
                    : job.Message.Body;
                if (body.Length > (withPdf ? 1024 : 4096))
                    return new(false, Error: "Telegram message exceeds its text/caption limit. Shorten the template or disable PDF delivery for a longer text message.");
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(job.Recipient), "chat_id");
                form.Add(new StringContent(body), withPdf ? "caption" : "text");
                if (c.MessageFormat == "HTML") form.Add(new StringContent("HTML"), "parse_mode");
                // User/customer text is escaped before adding the controlled bold heading.
                if (withPdf) { var file = new ByteArrayContent(job.Message.Pdf!); file.Headers.ContentType = new("application/pdf"); form.Add(file, "document", job.Message.FileName); }
                using var request = Request(c, withPdf ? "sendDocument" : "sendMessage", form);
                var telegramResult = (await Execute(request, c, token)).Result;
                return telegramResult.Success && telegramResult.MessageId.Length == 0
                    ? new(false, Error: "Telegram response did not confirm a message ID. Check provider history before retrying.") : telegramResult;
            }
            var templateName = job.NotificationType is "Invoice" or "Test message" ? c.TemplateName : c.AdminTemplateName;
            if (c.Provider != "Meta Cloud API" || !Regex.IsMatch(templateName, "^[a-z0-9_]+$")) return new(false, Error: "Select Meta Cloud API and an approved invoice or administrator template name.");
            var components = new List<object>();
            if (c.SendPdf && job.Message.Pdf != null)
            {
                using var media = new MultipartFormDataContent();
                media.Add(new StringContent("whatsapp"), "messaging_product");
                media.Add(new StringContent("application/pdf"), "type");
                var file = new ByteArrayContent(job.Message.Pdf); file.Headers.ContentType = new("application/pdf"); media.Add(file, "file", job.Message.FileName);
                using var upload = Request(c, "media", media);
                var (uploaded, json) = await Execute(upload, c, token);
                if (!uploaded.Success) return uploaded;
                if (!json.TryGetProperty("id", out var mediaId)) return new(false, Error: "Provider did not return a media ID.");
                components.Add(new { type = "header", parameters = new[] { new { type = "document", document = new { id = mediaId.GetString(), filename = job.Message.FileName } } } });
            }
            if (job.Message.TemplateParameters.Length > 0)
                components.Add(new { type = "body", parameters = job.Message.TemplateParameters.Select(p => new { type = "text", text = p }).ToArray() });
            using var send = Request(c, "messages", JsonContent.Create(new { messaging_product = "whatsapp", to = job.Recipient,
                type = "template", template = new { name = templateName, language = new { code = c.TemplateLanguage }, components } }));
            var metaResult = (await Execute(send, c, token)).Result;
            return metaResult.Success && metaResult.MessageId.Length == 0
                ? new(false, Error: "Meta response did not confirm a message ID. Check provider history before retrying.") : metaResult;
        }
        catch { return new(false, Error: "Provider send failed or timed out. Verify credentials and provider history before retrying."); }
    }
    private async Task<(NotificationResult Result, JsonElement Json)> Execute(HttpRequestMessage request, ChannelConfiguration c, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(c.TimeoutSeconds, 5, 120)));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        // Never include raw provider bodies or request URLs: Telegram URLs contain the token.
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        var json = document.RootElement.Clone();
        var limited = response.StatusCode == HttpStatusCode.TooManyRequests;
        if (!response.IsSuccessStatusCode || (json.TryGetProperty("ok", out var ok) && !ok.GetBoolean()))
        {
            var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
            if (json.TryGetProperty("parameters", out var parameters) && parameters.TryGetProperty("retry_after", out var retry)) seconds = retry.GetDouble();
            return (new(false, Error: $"Provider rejected request (HTTP {(int)response.StatusCode}). Check destination permissions, credentials, and approved WhatsApp template parameters.",
                RetrySafe: limited, RetryAfter: TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 86400))), json);
        }
        var id = "";
        if (json.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0) id = messages[0].GetProperty("id").GetString() ?? "";
        if (json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object && result.TryGetProperty("message_id", out var mid)) id = mid.ToString();
        return (new(true, id), json);
    }
}
