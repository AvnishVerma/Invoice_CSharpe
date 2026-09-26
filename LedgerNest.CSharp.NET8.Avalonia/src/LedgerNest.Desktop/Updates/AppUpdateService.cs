using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LedgerNest.Desktop.Updates;

public enum UpdateCheckState { NotConfigured, Current, Available, Failed }

public sealed record GlobalNotification(
    string Id,
    string Title,
    string Message,
    NotificationType Type = NotificationType.Information,
    DateTimeOffset? PublishedAt = null,
    DateTimeOffset? ExpiresAt = null);

public sealed record AppUpdateManifest(
    string Version,
    string? DownloadUrl = null,
    string? ReleaseNotes = null,
    DateTimeOffset? PublishedAt = null,
    bool Mandatory = false,
    IReadOnlyList<GlobalNotification>? Notifications = null);

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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
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

            return availableVersion.CompareTo(currentVersion) > 0
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

    public static string SerializeManifest(AppUpdateManifest manifest) => JsonSerializer.Serialize(manifest, JsonOptions);
}

public static class AppVersion
{
    private static readonly Assembly DesktopAssembly = typeof(AppVersion).Assembly;

    /// <summary>Gets the customer-facing version embedded in the deployed LedgerNest.Desktop DLL.</summary>
    public static string Display => TryFormat(ReadEmbeddedVersion(), out var version) ? version : "0.0.0";

    /// <summary>Gets the source revision embedded in the deployed DLL, when available.</summary>
    public static string BuildId
    {
        get
        {
            var parts = ReadEmbeddedVersion().Split('+', 2);
            return parts.Length == 2 ? parts[1] : "";
        }
    }

    /// <summary>Gets the numeric part of the deployed DLL version for release comparisons.</summary>
    public static string Current => Display;

    private static string ReadEmbeddedVersion()
    {
        var assemblyPath = DesktopAssembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
        {
            var productVersion = FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion;
            if (!string.IsNullOrWhiteSpace(productVersion)) return productVersion;
        }

        return DesktopAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? DesktopAssembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
    }

    private static bool TryFormat(string? value, out string version)
    {
        var cleanValue = value?.Split('+', 2)[0];
        if (Version.TryParse(cleanValue, out var parsed))
        {
            version = parsed.ToString(3);
            return true;
        }

        version = string.Empty;
        return false;
    }
}
