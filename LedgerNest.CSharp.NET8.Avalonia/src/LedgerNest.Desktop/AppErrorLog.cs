namespace LedgerNest.Desktop;

using Serilog;
using Serilog.Core;

internal static class AppErrorLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LedgerNest",
        "logs",
        "ledgernest-errors.log");

    private static readonly Lazy<Logger> Logger = new(() => new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(Path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
        .CreateLogger());

    public static void Write(Exception exception, string context)
    {
        try { Logger.Value.Error(exception, "{Context}", context); }
        catch { }
    }
}
