using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LedgerNest.Domain;
using Microsoft.Win32;

namespace LedgerNest.Infrastructure;

// Deliberately separate from invoice databases and their backup/restore lifecycle.
public sealed class FileLicenseStore(string directory) : ILicenseStore
{
    private string LicensePath => Path.Combine(directory, "activation.json");

    public StoredLicense? Read()
    {
        if (!File.Exists(LicensePath)) return null;
        if (new FileInfo(LicensePath).Length > LicenseFeatures.MaximumFileBytes * 2) throw new IOException("License storage exceeds its size limit.");
        return JsonSerializer.Deserialize<StoredLicense>(File.ReadAllText(LicensePath)) ?? throw new JsonException("License storage is empty.");
    }

    public void Write(StoredLicense license)
    {
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"activation-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(license));
            File.Move(temporary, LicensePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string GetDeviceId()
    {
        string? identity = null;
        if (OperatingSystem.IsWindows())
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            identity = key?.GetValue("MachineGuid") as string;
        }
        else if (OperatingSystem.IsLinux() && File.Exists("/etc/machine-id")) identity = File.ReadAllText("/etc/machine-id").Trim();
        if (string.IsNullOrWhiteSpace(identity))
        {
            // Fallback is per installation on platforms without an accessible OS machine ID.
            Directory.CreateDirectory(directory);
            var identityPath = Path.Combine(directory, "installation.id");
            if (!File.Exists(identityPath))
            {
                try
                {
                    using var stream = new FileStream(identityPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(stream); writer.Write(Guid.NewGuid().ToString("N"));
                }
                catch (IOException) when (File.Exists(identityPath)) { }
            }
            identity = File.ReadAllText(identityPath).Trim();
            if (!Guid.TryParse(identity, out _)) throw new IOException("Invalid installation identity.");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LicenseFeatures.ProductId + "\n" + identity.Trim().ToLowerInvariant())));
    }
}
