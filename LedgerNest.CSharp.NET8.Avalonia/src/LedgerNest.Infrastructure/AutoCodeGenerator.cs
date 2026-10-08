using System.Data;
using System.Globalization;
using LedgerNest.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Infrastructure;

/// <summary>Shared master-code settings and allocation; previews never reserve a number.</summary>
public sealed class AutoCodeGenerator(IDbContextFactory<LedgerNestDbContext> factory)
{
    private static string Key(string entity) => entity switch
    {
        "Customer" => "customerCode", "Product" => "productCode",
        _ => throw new ArgumentException("Only customer and product master codes are supported.", nameof(entity))
    };
    public AutoCodeSettings Load(string entity)
    {
        using var db = factory.CreateDbContext();
        return Read(db, entity);
    }
    private static IQueryable<string?> Codes(LedgerNestDbContext db, string entity, int excludingId = 0) => entity == "Customer"
        ? db.Customers.AsNoTracking().Where(item => item.Id != excludingId).Select(item => item.CustomerCode)
        : db.Products.AsNoTracking().Where(item => item.Id != excludingId).Select(item => item.ProductCode);

    private static AutoCodeSettings Read(LedgerNestDbContext db, string entity)
    {
        var key = Key(entity);
        var values = db.Settings.AsNoTracking().Where(item => item.Key.StartsWith(key)).ToDictionary(item => item.Key, item => item.Value);
        var settings = new AutoCodeSettings(values.GetValueOrDefault(key + "Prefix", entity == "Customer" ? "CUST" : "PROD"),
            ParseLong(values.GetValueOrDefault(key + "NextNumber", "1")),
            ParseWidth(values.GetValueOrDefault(key + "LeadingZeros", "5")),
            bool.TryParse(values.GetValueOrDefault(key + "AutoGenerate", "true"), out var enabled)
                ? enabled : throw new InvalidOperationException("Stored automatic-code enablement is invalid."));
        return settings with { NextNumber = AutoCodeRules.NextCompatible(settings, Codes(db, entity).AsEnumerable()) };
    }
    private static long ParseLong(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
        ? result : throw new InvalidOperationException("Stored automatic-code number is invalid.");
    private static int ParseWidth(string value)
    {
        var width = ParseLong(value);
        return width is >= 0 and <= 18 ? (int)width : throw new InvalidOperationException("Stored automatic-code padding must be between 0 and 18.");
    }
    private static void Put(LedgerNestDbContext db, string key, string value)
    {
        var item = db.Settings.Find(key);
        if (item == null) db.Settings.Add(new AppSetting { Key = key, Value = value }); else item.Value = value;
    }
    private static void Write(LedgerNestDbContext db, string entity, AutoCodeSettings settings)
    {
        var key = Key(entity);
        Put(db, key + "Prefix", settings.Prefix.Trim());
        Put(db, key + "NextNumber", settings.NextNumber.ToString(CultureInfo.InvariantCulture));
        Put(db, key + "LeadingZeros", settings.LeadingZeros.ToString(CultureInfo.InvariantCulture));
        Put(db, key + "AutoGenerate", settings.AutoGenerate.ToString().ToLowerInvariant());
    }
    public void SaveSettings(IReadOnlyDictionary<string, AutoCodeSettings> settings, string username)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var pair in settings) { Key(pair.Key); AutoCodeRules.Validate(pair.Value); }
        var authorization = new AuthorizationService(factory);
        if (!authorization.IsUserAllowedAsync(username, "Settings", "View").GetAwaiter().GetResult()
            || !authorization.IsUserAllowedAsync(username, "Settings", "Update").GetAwaiter().GetResult())
            throw new UnauthorizedAccessException("Your role cannot change code settings.");
        using var db = factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        foreach (var pair in settings)
        {
            var next = AutoCodeRules.NextCompatible(pair.Value, Codes(db, pair.Key).AsEnumerable());
            Write(db, pair.Key, pair.Value with { NextNumber = next });
        }
        db.SaveChanges(); transaction.Commit();
    }

    /// <summary>The supplied master persistence operation and sequence advancement share one transaction.</summary>
    public int SaveRecord(string entity, string manualCode, int sourceId, string username, Func<LedgerNestDbContext, string, int> persist)
    {
        Key(entity);
        ArgumentNullException.ThrowIfNull(manualCode);
        ArgumentNullException.ThrowIfNull(persist);
        if (sourceId < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var db = factory.CreateDbContext();
            try
            {
                var authorization = new AuthorizationService(factory);
                if (!authorization.IsUserAllowedAsync(username, entity, "View").GetAwaiter().GetResult()
                    || !authorization.IsUserAllowedAsync(username, entity, sourceId == 0 ? "Add" : "Update").GetAwaiter().GetResult())
                    throw new UnauthorizedAccessException("Your role cannot save this master record.");
                return db.Database.CreateExecutionStrategy().Execute(() =>
                {
                    using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
                    var configuration = Read(db, entity);
                    var code = manualCode.Trim();
                    if (sourceId != 0 && code.Length == 0)
                        code = entity == "Customer" ? db.Customers.Where(item => item.Id == sourceId).Select(item => item.CustomerCode).SingleOrDefault() ?? ""
                            : db.Products.Where(item => item.Id == sourceId).Select(item => item.ProductCode).SingleOrDefault() ?? "";
                    var generated = sourceId == 0 && code.Length == 0;
                    if (generated)
                    {
                        if (!configuration.AutoGenerate) throw new InvalidOperationException("Enter a code or enable automatic code generation in Company Information settings.");
                        if (configuration.NextNumber >= long.MaxValue - 1) throw new InvalidOperationException("Code numbers are exhausted. Choose another prefix and next number.");
                        code = AutoCodeRules.Format(configuration.Prefix, configuration.NextNumber, configuration.LeadingZeros);
                    }
                    if (code.Length > 1000 || code.Any(char.IsControl)) throw new InvalidOperationException("Code must contain at most 1000 printable characters.");
                    if (code.Length > 0 && Codes(db, entity, sourceId).Any(value => value == code))
                        throw new InvalidOperationException(entity + " ID is already in use.");
                    var id = persist(db, code);
                    if (id < 0) return id; // Disposal rolls back every write.
                    if (generated) Write(db, entity, configuration with { NextNumber = checked(configuration.NextNumber + 1) });
                    db.SaveChanges(); transaction.Commit();
                    return id;
                });
            }
            catch (Exception ex) when (attempt < 11 && (ex is DbUpdateConcurrencyException || ex is DbUpdateException || ex is SqliteException { SqliteErrorCode: 5 or 6 }))
            { Thread.Sleep(20 * (attempt + 1)); }
        }
        throw new InvalidOperationException("Code allocation could not complete after concurrent retries. Refresh and try again.");
    }
}
