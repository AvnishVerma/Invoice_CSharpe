using System.Diagnostics;
using LedgerNest.Desktop;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class AutoCodeProcessTests
{
    [Theory] [InlineData("Customer")] [InlineData("Product")]
    [Trait("Category", "Integration")]
    public async Task IndependentProcesses_ShareOneTransactionalCodeSequence(string entity)
    {
        var directory = Directory.CreateTempSubdirectory("ledgernest-code-process-");
        var path = Path.Combine(directory.FullName, "fixture.db");
        var factory = new ProcessFactory(path);
        using (var db = factory.CreateDbContext()) db.EnsureCurrentSchema();
        _ = new MainWindowViewModel(factory, path); // Initializes the existing Admin/role catalog.
        try
        {
            await Task.WhenAll(RunWorker(path, entity), RunWorker(path, entity));
            using var db = factory.CreateDbContext();
            var codes = entity == "Customer" ? db.Customers.Select(item => item.CustomerCode).ToArray() : db.Products.Select(item => item.ProductCode).ToArray();
            Assert.Equal(12, codes.Length); Assert.Equal(12, codes.Distinct().Count());
            Assert.Equal(13, new AutoCodeGenerator(factory).Load(entity).NextNumber);
        }
        finally { directory.Delete(true); }
    }

    private static async Task RunWorker(string path, string entity)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { typeof(Program).Assembly.Location, "--auto-code-worker", path, entity, "6" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await output + await error);
    }
    private sealed class ProcessFactory(string path) : IDbContextFactory<LedgerNestDbContext>
    {
        public LedgerNestDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
    }
}
