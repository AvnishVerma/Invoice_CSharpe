using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Infrastructure;

public interface ICommunicationProtection
{
    string Protect(string value);
    string Unprotect(string value);
}

// DPAPI user scope: database backups never contain reusable plaintext credentials or PDFs.
// Restoring on another Windows account requires reconfiguration; never silently fall back to plaintext.
public sealed class WindowsCommunicationProtection : ICommunicationProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);
    public string Protect(string value) => Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(value), true));
    public string Unprotect(string value) => Encoding.UTF8.GetString(Transform(Convert.FromBase64String(value), false));
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Communication credentials require Windows user-protected storage.");
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var success = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) throw new CryptographicException("Communication data cannot be unlocked by this Windows account.");
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }
}

public sealed class CommunicationStore(IDbContextFactory<LedgerNestDbContext> factory, ICommunicationProtection protection) : ICommunicationStore
{
    private const string ConfigurationKey = "communication:v1:configuration";
    private const string JobPrefix = "communication:v1:job:";
    private const string PayloadPrefix = "communication:v1:payload:";
    private static readonly JsonSerializerOptions MetadataOptions = CreateMetadataOptions();
    private static JsonSerializerOptions CreateMetadataOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type == typeof(DeliveryJob))
                info.Properties.Single(p => p.Name == nameof(DeliveryJob.Message)).ShouldSerialize = (_, _) => false;
        });
        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }
    private const string ConsentPrefix = "communication:v1:consent:";
    private T Read<T>(string key, Func<T> empty)
    {
        using var db = factory.CreateDbContext();
        var data = db.Settings.AsNoTracking().SingleOrDefault(s => s.Key == key)?.Value;
        return data == null ? empty() : JsonSerializer.Deserialize<T>(protection.Unprotect(data)) ?? empty();
    }
    private void Write<T>(string key, T value)
    {
        using var db = factory.CreateDbContext();
        var setting = db.Settings.SingleOrDefault(s => s.Key == key);
        var data = protection.Protect(JsonSerializer.Serialize(value));
        if (setting == null) db.Settings.Add(new AppSetting { Key = key, Value = data }); else setting.Value = data;
        db.SaveChanges();
    }
    public CommunicationConfiguration LoadConfiguration() => Read(ConfigurationKey, () => new CommunicationConfiguration());
    public void SaveConfiguration(CommunicationConfiguration configuration) => Write(ConfigurationKey, configuration);
    public CustomerCommunicationConsent Consent(int customerId) => Read(ConsentPrefix + customerId, () => new CustomerCommunicationConsent { CustomerId = customerId });
    public void SaveConsent(CustomerCommunicationConsent consent) => Write(ConsentPrefix + consent.CustomerId, consent);
    public IReadOnlyList<DeliveryJob> History()
    {
        using var db = factory.CreateDbContext();
        return db.Settings.AsNoTracking().Where(s => s.Key.StartsWith(JobPrefix)).Select(s => s.Value).ToArray()
            .Select(value => JsonSerializer.Deserialize<DeliveryJob>(protection.Unprotect(value))!).OrderByDescending(j => j.CreatedAt).ToArray();
    }
    private static string Key(DeliveryJob job) => JobPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(job.DeduplicationKey)));
    public bool Enqueue(DeliveryJob job)
    {
        using var db = factory.CreateDbContext();
        var key = Key(job);
        if (db.Settings.Any(s => s.Key == key)) return false;
        db.Settings.Add(new AppSetting { Key = key, Value = protection.Protect(JsonSerializer.Serialize(job, MetadataOptions)) });
        db.Settings.Add(new AppSetting { Key = PayloadPrefix + key[JobPrefix.Length..], Value = protection.Protect(JsonSerializer.Serialize(job.Message)) });
        try { db.SaveChanges(); return true; }
        catch (DbUpdateException) { using var check = factory.CreateDbContext(); if (check.Settings.Any(s => s.Key == key)) return false; throw; }
    }
    public bool TryClaim(DeliveryJob job)
    {
        using var db = factory.CreateDbContext(); var key = Key(job);
        var data = db.Settings.AsNoTracking().Single(s => s.Key == key).Value;
        var saved = JsonSerializer.Deserialize<DeliveryJob>(protection.Unprotect(data))!;
        if (saved.State is not (DeliveryState.Pending or DeliveryState.RetryScheduled)) return false;
        job.Message = ReadMessage(job);
        job.State = DeliveryState.Processing; job.LastAttemptAt = DateTimeOffset.UtcNow;
        var changed = protection.Protect(JsonSerializer.Serialize(job, MetadataOptions));
        return db.Settings.Where(s => s.Key == key && s.Value == data).ExecuteUpdate(setters => setters.SetProperty(s => s.Value, changed)) == 1;
    }
    public DeliveryMessage ReadMessage(DeliveryJob job) => Read(PayloadPrefix + Key(job)[JobPrefix.Length..], () => new DeliveryMessage());
    public void Save(DeliveryJob job)
    {
        using var db = factory.CreateDbContext(); var key = Key(job);
        db.Settings.Single(s => s.Key == key).Value = protection.Protect(JsonSerializer.Serialize(job, MetadataOptions));
        db.SaveChanges();
    }
    public bool IsInvoiceSendable(int invoiceId)
    {
        using var db = factory.CreateDbContext(); var invoice = db.Invoices.AsNoTracking().SingleOrDefault(i => i.Id == invoiceId);
        return invoice != null && invoice.Type == "Invoice" && invoice.DeletedAt == null && !invoice.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) && !InvoiceStatusRules.IsVoided(invoice.Status);
    }
    public string BuildReport(DateTime from, DateTime until)
    {
        using var db = factory.CreateDbContext();
        var invoices = db.Invoices.AsNoTracking().Where(i => i.Type == "Invoice" && i.DeletedAt == null && i.InvoiceDate >= from && i.InvoiceDate < until).ToArray();
        var active = invoices.Where(i => !InvoiceStatusRules.IsVoided(i.Status)).ToArray();
        var payments = db.Payments.AsNoTracking().Where(p => p.PaymentDate >= from && p.PaymentDate < until).ToArray();
        var history = History();
        bool InPeriod(DateTimeOffset? stamp) => stamp is { } value && value.LocalDateTime >= from && value.LocalDateTime < until;
        var submitted = history.Count(j => j.NotificationType == "Invoice" && j.State == DeliveryState.Sent && InPeriod(j.SentAt));
        var failed = history.Count(j => j.NotificationType == "Invoice" && j.State == DeliveryState.Failed && InPeriod(j.LastAttemptAt ?? j.CreatedAt));
        var errors = history.Count(j => j.State is DeliveryState.Failed or DeliveryState.RetryScheduled &&
            j.Error.Length > 0 && InPeriod(j.LastAttemptAt ?? j.CreatedAt));
        // Currency comes from each saved snapshot, never from today's company settings.
        static string Currency(Invoice i) => i.Snapshot?.Currency ?? "Unspecified (legacy)";
        var summaries = active.GroupBy(Currency).Select(group =>
        {
            var top = group.GroupBy(i => i.CustomerName).OrderByDescending(g => g.Sum(i => i.GrandTotal)).Take(5).Select(g => $"{g.Key}: {g.Sum(i => i.GrandTotal):0.00}");
            var trend = group.GroupBy(i => i.InvoiceDate.Date).OrderBy(g => g.Key).Select(g => $"{g.Key:yyyy-MM-dd}: {g.Sum(i => i.GrandTotal):0.00}");
            return $"Currency: {group.Key}\nSales: {group.Sum(i => i.GrandTotal):0.00}\nPaid against these invoices: {group.Sum(i => i.PaidAmount):0.00}\nOutstanding: {group.Sum(i => InvoiceStatusRules.Outstanding(i)):0.00}\nTop customers:\n{string.Join("\n", top)}\nDaily sales:\n{string.Join("\n", trend)}";
        });
        var paymentIds = payments.Select(p => p.InvoiceId).Distinct().ToArray();
        var currencies = db.Invoices.AsNoTracking().Where(i => paymentIds.Contains(i.Id)).ToArray().ToDictionary(i => i.Id, Currency);
        var paymentSummary = payments.GroupBy(p => (Currency: currencies.GetValueOrDefault(p.InvoiceId, "Unspecified"), p.Method))
            .Select(g => $"{g.Key.Currency}, {g.Key.Method}: {g.Sum(p => p.Amount):0.00}");
        return $"Period {from:yyyy-MM-dd} to {until.AddDays(-1):yyyy-MM-dd}\nInvoices by invoice date: {invoices.Length}\nPaid invoices: {active.Count(i => InvoiceStatusRules.Outstanding(i) == 0)}\nUnpaid invoices: {active.Count(i => InvoiceStatusRules.Outstanding(i) > 0)}\nVoided: {invoices.Count(i => InvoiceStatusRules.IsVoided(i.Status))}\nCustomers: {db.Customers.Count()}\nSubmitted invoice deliveries: {submitted}\nFailed invoice deliveries: {failed}\nJobs with integration errors: {errors}\n{string.Join("\n", summaries)}\nPayments received in period:\n{string.Join("\n", paymentSummary)}";
    }
}
