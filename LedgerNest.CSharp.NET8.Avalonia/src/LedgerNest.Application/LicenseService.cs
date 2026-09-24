using LedgerNest.Domain;

namespace LedgerNest.Application;

public sealed class LicenseService(LicenseVerifier verifier, ILicenseStore store, string deviceId, TimeProvider? clock = null) : ILicenseService
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly object sync = new();
    private DateTimeOffset lastObservedUtc;
    public string DeviceId { get; } = deviceId;

    public LicenseStatus GetStatus()
    {
        lock (sync)
        {
            if (!verifier.IsConfigured) return verifier.Verify("", DeviceId, clock.GetUtcNow());
            try
            {
                var saved = store.Read();
                if (saved is null) return new(LicenseState.Missing, "Import a license file to enable business changes.");
                var now = clock.GetUtcNow();
                if (saved.LastSeenUtc - now > TimeSpan.FromMinutes(5) || lastObservedUtc - now > TimeSpan.FromMinutes(5)) return ClockError();
                lastObservedUtc = now > lastObservedUtc ? now : lastObservedUtc;
                var status = verifier.Verify(saved.Document, DeviceId, now);
                if (status.State is LicenseState.Active or LicenseState.Trial or LicenseState.Expired && now - saved.LastSeenUtc >= TimeSpan.FromMinutes(1))
                    store.Write(saved with { LastSeenUtc = now });
                return status;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentOutOfRangeException)
            { return new(LicenseState.StorageError, "License storage could not be read or updated. Check file permissions and import your license again."); }
        }
    }

    public LicenseStatus Activate(string document)
    {
        lock (sync)
        {
            var now = clock.GetUtcNow();
            var candidate = verifier.Verify(document, DeviceId, now);
            if (candidate.State is not (LicenseState.Active or LicenseState.Trial)) return candidate;
            try
            {
                // Importing the same file must not reset rollback detection.
                StoredLicense? saved;
                try { saved = store.Read(); }
                catch (System.Text.Json.JsonException) { saved = null; } // A verified replacement can repair malformed storage.
                if (lastObservedUtc - now > TimeSpan.FromMinutes(5) || saved is not null && saved.LastSeenUtc - now > TimeSpan.FromMinutes(5)) return ClockError();
                store.Write(new StoredLicense(document, now));
                lastObservedUtc = now;
                return candidate;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentOutOfRangeException)
            { return new(LicenseState.StorageError, "The license could not be saved. The current activation has not been replaced."); }
        }
    }

    private static LicenseStatus ClockError() => new(LicenseState.ClockError, "The device clock moved backwards. Correct the date and time before using this license.");
}
