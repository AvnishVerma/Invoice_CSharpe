using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LedgerNest.Domain;

namespace LedgerNest.Application;

public sealed class LicenseVerifier
{
    private readonly byte[]? publicKey;
    public bool IsConfigured => publicKey is not null;

    public LicenseVerifier(string publicKeyPem)
    {
        try
        {
            // A desktop trust anchor must contain only a public key.
            var pem = PemEncoding.Find(publicKeyPem);
            if (publicKeyPem[pem.Label] != "PUBLIC KEY") return;
            var bytes = Convert.FromBase64String(publicKeyPem[pem.Base64Data]);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(bytes, out var bytesRead);
            if (rsa.KeySize >= 2048 && bytesRead == bytes.Length) publicKey = rsa.ExportSubjectPublicKeyInfo();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or CryptographicException) { }
    }

    public LicenseStatus Verify(string document, string deviceId, DateTimeOffset now)
    {
        if (!IsConfigured) return new(LicenseState.NotConfigured, "License verification is not configured in this build. Contact your software provider.");
        if (string.IsNullOrWhiteSpace(document)) return new(LicenseState.Missing, "Import a license file to enable business changes.");
        if (Encoding.UTF8.GetByteCount(document) > LicenseFeatures.MaximumFileBytes) return Invalid();
        try
        {
            var envelope = JsonSerializer.Deserialize<SignedLicense>(document);
            if (envelope is null || string.IsNullOrEmpty(envelope.Payload) || string.IsNullOrEmpty(envelope.Signature)) return Invalid();
            var payload = Convert.FromBase64String(envelope.Payload);
            var signature = Convert.FromBase64String(envelope.Signature);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey!, out _);
            if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) return Invalid();
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty(nameof(LicenseClaims.Product), out _)
                || !json.RootElement.TryGetProperty(nameof(LicenseClaims.Version), out _) || !json.RootElement.TryGetProperty(nameof(LicenseClaims.Kind), out _)) return Invalid();
            var claims = JsonSerializer.Deserialize<LicenseClaims>(payload);
            if (claims is null || claims.Version != 1 || claims.Product != LicenseFeatures.ProductId
                || !Guid.TryParse(claims.LicenseId, out _) || string.IsNullOrWhiteSpace(claims.Customer) || claims.Customer.Length > 200
                || string.IsNullOrWhiteSpace(claims.DeviceId) || claims.Kind is not ("Trial" or "Paid")
                || claims.Features is null || claims.Features.Length is < 1 or > 32 || claims.Features.Any(string.IsNullOrWhiteSpace)
                || claims.IssuedAtUtc == default || claims.NotBeforeUtc < claims.IssuedAtUtc
                || claims.ExpiresAtUtc <= claims.NotBeforeUtc
                || claims.Kind == "Trial" && (claims.ExpiresAtUtc is null || claims.ExpiresAtUtc > claims.IssuedAtUtc.AddDays(30))) return Invalid();
            if (!string.Equals(claims.DeviceId, deviceId, StringComparison.Ordinal))
                return new(LicenseState.WrongDevice, "This license was issued for another device.");
            if (now < claims.NotBeforeUtc || now < claims.IssuedAtUtc)
                return new(LicenseState.NotYetValid, "This license is not yet valid. Check the device date and time.", claims);
            if (claims.ExpiresAtUtc is { } expiry && now >= expiry)
                return new(LicenseState.Expired, "This license has expired. Import a renewed license to continue making business changes.", claims);
            return new(claims.Kind == "Trial" ? LicenseState.Trial : LicenseState.Active,
                claims.Kind == "Trial" ? "Trial license is active." : "License is active.", claims);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or ArgumentException)
        { return Invalid(); }
    }

    private static LicenseStatus Invalid() => new(LicenseState.Invalid, "The license file is invalid or its signature could not be verified.");
}
