using System.Security.Cryptography;
using System.Text.Json;
using LedgerNest.Domain;

namespace LedgerNest.LicensePublisher;

// Publisher-only code. Never reference this project from the customer application.
public static class LicenseIssuer
{
    public sealed record KeyPair(string EncryptedPrivateKey, string PublicKey);
    public sealed record IssuedLicense(LicenseClaims Claims, string Document);

    public static KeyPair GenerateKeys(string password)
    {
        if (password.Length < 16) throw new ArgumentException("Use a signing password of at least 16 characters.");
        using var rsa = RSA.Create(3072);
        return new(rsa.ExportEncryptedPkcs8PrivateKeyPem(password,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 200_000)),
            rsa.ExportSubjectPublicKeyInfoPem());
    }

    public static void ValidateRequest(string customer, string device, bool trial, bool perpetual, int days)
    {
        if (string.IsNullOrWhiteSpace(customer) || customer.Trim().Length > 200)
            throw new ArgumentException("Enter a customer name of 1–200 characters.");
        if (device.Trim().Length != 64 || device.Trim().Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Paste the complete 64-character Device ID from the customer's License settings.");
        if (trial && perpetual) throw new ArgumentException("Trial licenses must have an expiry date.");
        if (!perpetual && (days < 1 || days > (trial ? 30 : 36500)))
            throw new ArgumentException(trial ? "Trial duration must be 1–30 days." : "Paid duration must be 1–36500 days.");
    }

    public static IssuedLicense Issue(string encryptedKey, string password, string customer, string device,
        bool trial, bool perpetual, int days, DateTimeOffset? issuedAt = null)
    {
        ValidateRequest(customer, device, trial, perpetual, days);
        using var rsa = RSA.Create();
        rsa.ImportFromEncryptedPem(encryptedKey, password);
        if (rsa.KeySize < 2048) throw new ArgumentException("The signing key must be at least 2048 bits.");
        var now = (issuedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var claims = new LicenseClaims
        {
            LicenseId = Guid.NewGuid().ToString(), Customer = customer.Trim(), DeviceId = device.Trim().ToUpperInvariant(),
            Kind = trial ? "Trial" : "Paid", IssuedAtUtc = now, NotBeforeUtc = now,
            ExpiresAtUtc = perpetual ? null : now.AddDays(days), Features = [LicenseFeatures.BusinessWrite]
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(claims);
        var document = JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(payload),
            Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))));
        return new(claims, document);
    }

    public static void WriteNew(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    public static void SaveKeys(string privatePath, string publicPath, KeyPair pair)
    {
        if (Path.GetFullPath(privatePath).Equals(Path.GetFullPath(publicPath), StringComparison.OrdinalIgnoreCase)
            || File.Exists(privatePath) || File.Exists(publicPath))
            throw new ArgumentException("Choose two different output paths that do not already exist.");
        // If saving the public key fails, retain the encrypted private key for recovery.
        WriteNew(privatePath, pair.EncryptedPrivateKey);
        try { WriteNew(publicPath, pair.PublicKey); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"The encrypted private key was saved to {privatePath}, but the public key could not be saved. Use Export public key to recover it. {ex.Message}", ex);
        }
    }

    public static string ExportPublicKey(string encryptedKey, string password)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromEncryptedPem(encryptedKey, password);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }
}
