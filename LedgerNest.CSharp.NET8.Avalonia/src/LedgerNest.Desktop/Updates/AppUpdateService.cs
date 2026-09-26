using System.Reflection;
using System.Text.Json;

namespace LedgerNest.Desktop.Updates;

public enum UpdateCheckState { NotConfigured, Current, Available, Failed }

public sealed record AppUpdateManifest(
    string Version,
    string? DownloadUrl = null,
    string? ReleaseNotes = null,
    DateTimeOffset? PublishedAt = null,
    bool Mandatory = false);

public sealed record UpdateCheckResult(
    UpdateCheckState State,
    string CurrentVersion,
    string Message,
    AppUpdateManifest? Manifest = null)
{
    public bool IsUpdateAvailable => State == UpdateCheckState.Available;
}

public interface IAppUpdateService
{
    Task<UpdateCheckResult> CheckAsync(string? manifestUrl, CancellationToken cancellationToken = default);
}

/// <summary>Checks a publisher-controlled HTTPS manifest. Download and installation remain explicit user actions.</summary>
public sealed class HttpAppUpdateService : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient httpClient;

    public HttpAppUpdateService(HttpClient? httpClient = null) =>
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<UpdateCheckResult> CheckAsync(string? manifestUrl, CancellationToken cancellationToken = default)
    {
        var current = AppVersion.Current;
        if (string.IsNullOrWhiteSpace(manifestUrl))
            return new(UpdateCheckState.NotConfigured, current, "Update checks are not configured.");
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new(UpdateCheckState.Failed, current, "The update manifest URL must use HTTPS.");

        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(UpdateCheckState.Failed, current, $"The update service returned {(int)response.StatusCode}.");

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (payload.Length > 128 * 1024)
                return new(UpdateCheckState.Failed, current, "The update manifest is too large.");
            var manifest = JsonSerializer.Deserialize<AppUpdateManifest>(payload, JsonOptions);
            if (manifest == null || !Version.TryParse(manifest.Version, out var availableVersion))
                return new(UpdateCheckState.Failed, current, "The update manifest contains an invalid version.");
            if (!Version.TryParse(current, out var currentVersion))
                return new(UpdateCheckState.Failed, current, "The installed application version is invalid.");
            if (!string.IsNullOrWhiteSpace(manifest.DownloadUrl) &&
                (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps))
                return new(UpdateCheckState.Failed, current, "The update manifest contains an unsafe download URL.");

            return availableVersion > currentVersion
                ? new(UpdateCheckState.Available, current, $"Version {manifest.Version} is available.", manifest)
                : new(UpdateCheckState.Current, current, "You are using the latest version.", manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(UpdateCheckState.Failed, current, "Unable to check for updates. Check your connection and update URL.");
        }
    }
}

public static class AppVersion
{
    public static string Current =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";
}
