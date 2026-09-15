namespace LedgerNest.Desktop;

using Serilog;
using Serilog.Core;

internal static class AppErrorLog
{
    public static string Path { get; } = BuildPath();

    private static readonly Lazy<Logger> Logger = new(() => new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(Path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
        .CreateLogger());

    // Performs the write action for this screen or workflow.
    public static void Write(Exception exception, string context)
    {
        try { Logger.Value.Error(exception, "{Context}", context); }
        catch { }
    }

    // Performs the build path action for this screen or workflow.
    private static string BuildPath()
    {
        var root = OperatingSystem.IsMacOS()
            ? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = System.IO.Path.Combine(root, "LedgerNest", "logs");
        Directory.CreateDirectory(directory);
        return System.IO.Path.Combine(directory, "ledgernest-errors.log");
    }
}
