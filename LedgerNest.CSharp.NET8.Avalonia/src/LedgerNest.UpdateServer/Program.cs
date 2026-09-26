using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<PublisherStore>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/manifest", (PublisherStore store) => Results.Json(store.GetManifest()));

app.MapGet("/admin", () => Results.Content(AdminConsole.Page, "text/html"));
app.MapGet("/api/admin/state", (HttpRequest request, PublisherStore store) =>
    Authorize(request, store) ? Results.Json(store.GetState()) : Unauthorized(store));
app.MapPost("/api/admin/publish", async (HttpRequest request, PublisherStore store) =>
{
    if (!Authorize(request, store)) return Unauthorized(store);
    var command = await request.ReadFromJsonAsync<PublishCommand>();
    var error = PublisherStore.Validate(command);
    if (error != null) return Results.BadRequest(new { error });
    store.Publish(command!);
    return Results.Ok(new { message = "Published. Connected LedgerNest apps will receive this on their next check.", manifest = store.GetManifest() });
});

app.Run();

static bool Authorize(HttpRequest request, PublisherStore store)
{
    var key = request.Headers["X-LedgerNest-Admin-Key"].FirstOrDefault()
        ?? request.Query["key"].FirstOrDefault();
    return store.HasAdminKey && string.Equals(key, store.AdminKey, StringComparison.Ordinal);
}

static IResult Unauthorized(PublisherStore store) => store.HasAdminKey
    ? Results.Unauthorized()
    : Results.Problem("Set Publisher:ApiKey or the LEDGERNEST_PUBLISHER_KEY environment variable before enabling administration.", statusCode: StatusCodes.Status503ServiceUnavailable);

public enum AnnouncementType { Information, Update, Warning, Error }
public sealed record Announcement(string Id, string Title, string Message, AnnouncementType Type, DateTimeOffset PublishedAt);
public sealed record UpdateManifest(string Version, string? DownloadUrl, string? ReleaseNotes, DateTimeOffset PublishedAt, bool Mandatory, IReadOnlyList<Announcement> Notifications);
public sealed record PublishCommand(string? Version, string? DownloadUrl, string? ReleaseNotes, bool Mandatory, string? AnnouncementTitle, string? AnnouncementMessage, AnnouncementType AnnouncementType = AnnouncementType.Information);
public sealed record PublisherState(UpdateManifest Manifest);

public sealed class PublisherStore
{
    private readonly object gate = new();
    private readonly string stateFile;
    public string AdminKey { get; }
    public bool HasAdminKey => !string.IsNullOrWhiteSpace(AdminKey);

    public PublisherStore(IConfiguration configuration, IHostEnvironment environment)
    {
        AdminKey = Environment.GetEnvironmentVariable("LEDGERNEST_PUBLISHER_KEY") ?? configuration["Publisher:ApiKey"] ?? "";
        var configuredPath = configuration["Publisher:StateFile"] ?? "App_Data/publisher-state.json";
        stateFile = Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public PublisherState GetState()
    {
        lock (gate)
        {
            if (!File.Exists(stateFile)) return new(new UpdateManifest("0.0.0", null, null, DateTimeOffset.UtcNow, false, []));
            return JsonSerializer.Deserialize<PublisherState>(File.ReadAllText(stateFile), JsonOptions) ?? new(new UpdateManifest("0.0.0", null, null, DateTimeOffset.UtcNow, false, []));
        }
    }

    public UpdateManifest GetManifest() => GetState().Manifest;

    public void Publish(PublishCommand command)
    {
        lock (gate)
        {
            var current = GetState().Manifest;
            var notifications = current.Notifications.Where(item => item.Id != "latest-announcement").ToList();
            if (!string.IsNullOrWhiteSpace(command.AnnouncementTitle))
                notifications.Insert(0, new Announcement("latest-announcement", command.AnnouncementTitle.Trim(), command.AnnouncementMessage!.Trim(), command.AnnouncementType, DateTimeOffset.UtcNow));
            var manifest = new UpdateManifest(command.Version!.Trim(), command.DownloadUrl?.Trim(), command.ReleaseNotes?.Trim(), DateTimeOffset.UtcNow, command.Mandatory, notifications);
            Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
            File.WriteAllText(stateFile, JsonSerializer.Serialize(new PublisherState(manifest), JsonOptions));
        }
    }

    public static string? Validate(PublishCommand? command)
    {
        if (command == null || !Version.TryParse(command.Version, out _)) return "Version must be a numeric value such as 4.5.0.";
        if (!string.IsNullOrWhiteSpace(command.DownloadUrl) && (!Uri.TryCreate(command.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)) return "Download URL must use HTTPS.";
        if (string.IsNullOrWhiteSpace(command.AnnouncementTitle) != string.IsNullOrWhiteSpace(command.AnnouncementMessage)) return "Provide both announcement title and message, or neither.";
        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
}

public static class AdminConsole
{
    public const string Page = """
<!doctype html><html><head><meta charset='utf-8'><title>LedgerNest Publisher Console</title><style>body{font:16px system-ui;background:#101820;color:#f5f7fb;margin:0}.wrap{max-width:760px;margin:48px auto;padding:28px;background:#1d2b38;border-radius:12px}input,textarea,select{box-sizing:border-box;width:100%;padding:10px;margin:6px 0 16px;border-radius:6px;border:1px solid #52687a;background:#0f1720;color:white}button{padding:11px 18px;background:#087f5b;color:white;border:0;border-radius:6px;font-weight:700}.hint{color:#a9bbc9;font-size:13px}#result{white-space:pre-wrap;margin-top:18px}</style></head><body><main class='wrap'><h1>LedgerNest Publisher Console</h1><p class='hint'>Publishing replaces the global update manifest. Clients retrieve it at startup and every 30 minutes.</p><label>Admin API key</label><input id='key' type='password' autocomplete='current-password'><label>Release version</label><input id='version' placeholder='4.5.0'><label>HTTPS download URL</label><input id='url' placeholder='https://downloads.example.com/LedgerNest-4.5.0.exe'><label>Release notes</label><textarea id='notes'></textarea><label>Announcement title (optional)</label><input id='title'><label>Announcement message (optional)</label><textarea id='message'></textarea><label>Severity</label><select id='type'><option>Information</option><option>Update</option><option>Warning</option><option>Error</option></select><label><input id='mandatory' type='checkbox' style='width:auto'> Mandatory update</label><br><button onclick='publish()'>Publish globally</button><pre id='result'></pre></main><script>async function publish(){const body={version:version.value,downloadUrl:url.value,releaseNotes:notes.value,mandatory:mandatory.checked,announcementTitle:title.value,announcementMessage:message.value,announcementType:type.value};const r=await fetch('/api/admin/publish',{method:'POST',headers:{'Content-Type':'application/json','X-LedgerNest-Admin-Key':key.value},body:JSON.stringify(body)});result.textContent=JSON.stringify(await r.json(),null,2);}</script></body></html>
""";
}
