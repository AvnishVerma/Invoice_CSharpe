using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Infrastructure;

public sealed record CompanyProfile(string Id, string Name, DatabaseProfile Database, DateTime CreatedAt, bool IsOriginal = false);
public sealed record CompanyRegistry(string ActiveCompanyId, CompanyProfile[] Companies);

// Device-level configuration; company business data remains in separate databases.
public sealed class CompanyRegistryService
{
    public const string IdentitySetting = "company.identity";
    private readonly string directory;
    private readonly string registryPath;
    public CompanyRegistryService(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        registryPath = Path.Combine(this.directory, "companies.json");
    }

    public CompanyRegistry Initialize(DatabaseProfile original)
    {
        Directory.CreateDirectory(directory);
        using var registryLock = AcquireLock();
        if (File.Exists(registryPath)) return Read();
        var id = Guid.NewGuid().ToString("N");
        var name = "My Company";
        // Adopt the existing database in place, including its identity if already assigned.
        if (original.Provider == DatabaseProvider.Sqlite)
        {
            var path = new SqliteConnectionStringBuilder(original.ConnectionString).DataSource;
            if (File.Exists(path))
            {
                var options = new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite(original.ConnectionString).Options;
                using var db = new LedgerNestDbContext(options);
                db.EnsureCurrentSchema();
                var savedId = db.Settings.Find(IdentitySetting)?.Value;
                if (Guid.TryParse(savedId, out var existingId)) id = existingId.ToString("N");
                name = db.CompanyInfos.AsNoTracking().Select(company => company.Name).FirstOrDefault() ?? name;
            }
        }
        var registry = new CompanyRegistry(id, [new(id, string.IsNullOrWhiteSpace(name) ? "My Company" : name, original, DateTime.UtcNow, true)]);
        Write(registry);
        return registry;
    }

    public CompanyRegistry Read()
    {
        try
        {
            var registry = JsonSerializer.Deserialize<CompanyRegistry>(File.ReadAllText(registryPath));
            if (registry == null || registry.Companies == null || registry.Companies.Length == 0
                || registry.Companies.Any(company => company == null || !Guid.TryParseExact(company.Id, "N", out _) || company.Database == null
                    || !Enum.IsDefined(company.Database.Provider) || string.IsNullOrWhiteSpace(company.Database.ConnectionString) || string.IsNullOrWhiteSpace(company.Name))
                || registry.Companies.Select(company => company.Id).Distinct().Count() != registry.Companies.Length
                || !registry.Companies.Any(company => company.Id == registry.ActiveCompanyId))
                throw new InvalidDataException("The company registry is invalid. Restore its previous valid copy before continuing.");
            return registry;
        }
        catch (JsonException ex) { throw new InvalidDataException("The company registry could not be read. It has not been overwritten.", ex); }
    }

    public CompanyProfile Create(string name)
    {
        using var registryLock = AcquireLock();
        var registry = Read();
        name = ValidateName(name, registry.Companies);
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory, "Companies", id, "ledgernest.db");
        var profile = new CompanyProfile(id, name, DatabaseProfile.LocalSqlite(path), DateTime.UtcNow);
        // Registration is committed only after the separate database is initialized.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var options = new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite(profile.Database.ConnectionString).Options;
        using (var db = new LedgerNestDbContext(options))
        {
            db.EnsureCurrentSchema();
            db.Settings.Add(new LedgerNest.Domain.AppSetting { Key = IdentitySetting, Value = id });
            db.CompanyInfos.Add(new LedgerNest.Domain.CompanyInfo { Name = name });
            db.SaveChanges();
        }
        Write(registry with { Companies = [.. registry.Companies, profile] });
        return profile;
    }

    public CompanyProfile Rename(string id, string name)
    {
        using var registryLock = AcquireLock();
        var registry = Read();
        var company = Find(registry, id);
        var renamed = company with { Name = ValidateName(name, registry.Companies.Where(item => item.Id != id)) };
        Write(registry with { Companies = registry.Companies.Select(item => item.Id == id ? renamed : item).ToArray() });
        return renamed;
    }

    public void Activate(string id)
    {
        using var registryLock = AcquireLock();
        var registry = Read();
        Find(registry, id);
        Write(registry with { ActiveCompanyId = id });
    }

    public void Delete(string id)
    {
        using var registryLock = AcquireLock();
        var registry = Read();
        Find(registry, id);
        if (registry.Companies.Length == 1) throw new InvalidOperationException("The last company cannot be deleted.");
        if (id == registry.ActiveCompanyId) throw new InvalidOperationException("Switch to another company before deleting this company.");
        // Remove access through the registry while retaining a recoverable database.
        // Never drop externally configured MySQL/SQL Server databases or the adopted original file.
        var archive = Path.Combine(directory, "DeletedCompanies");
        Directory.CreateDirectory(archive);
        WriteJsonAtomically(Path.Combine(archive, id + ".json"), Find(registry, id));
        Write(registry with { Companies = registry.Companies.Where(item => item.Id != id).ToArray() });
    }

    public string BackupDirectory(string id)
    {
        Find(Read(), id);
        var path = Path.Combine(directory, "Backups", id);
        Directory.CreateDirectory(path);
        return path;
    }

    public string BackupFileName(string id, string extension, DateTime timestamp)
    {
        var company = Find(Read(), id);
        if (extension is not ("json" or "invoicedb")) throw new ArgumentException("Unsupported backup format.", nameof(extension));
        var safeName = new string(company.Name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray()).Trim('_');
        if (safeName.Length == 0) safeName = "Company";
        return $"{safeName[..Math.Min(safeName.Length, 60)]}_{id}_{timestamp:yyyyMMdd_HHmmss}.{extension}";
    }

    private static CompanyProfile Find(CompanyRegistry registry, string id) => registry.Companies.SingleOrDefault(item => item.Id == id)
        ?? throw new InvalidOperationException("This company no longer exists. Refresh the company list.");

    private static string ValidateName(string name, IEnumerable<CompanyProfile> companies)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 1 or > 100) throw new ArgumentException("Enter a company name of 1 to 100 characters.", nameof(name));
        if (companies.Any(company => company.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A company with this name already exists.");
        return name;
    }

    private void Write(CompanyRegistry registry) => WriteJsonAtomically(registryPath, registry);

    private IDisposable AcquireLock()
    {
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            OperatingSystem.IsWindows() ? registryPath.ToUpperInvariant() : registryPath)));
        var mutex = new Mutex(false, "LedgerNestCompanies_" + key);
        try
        {
            try { if (!mutex.WaitOne(TimeSpan.FromSeconds(30))) throw new TimeoutException("The company registry is busy. Try again."); }
            catch (AbandonedMutexException) { } // Ownership is acquired when an abandoned mutex is reported.
            return new RegistryLock(mutex);
        }
        catch { mutex.Dispose(); throw; }
    }

    private sealed class RegistryLock(Mutex mutex) : IDisposable
    {
        public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
    }

    private static void WriteJsonAtomically<T>(string path, T value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
