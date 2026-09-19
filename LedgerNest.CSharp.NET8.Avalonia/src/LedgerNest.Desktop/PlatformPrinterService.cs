using System.ComponentModel;
using System.Diagnostics;

namespace LedgerNest.Desktop;

// Provides printer discovery and job submission through the native printing system on each desktop platform.
internal static class PlatformPrinterService
{
    public const string DefaultPrinter = "System default printer";
    private const string LegacyWindowsDefaultPrinter = "Windows default printer";

    // Returns installed printer destinations for the active operating system.
    public static string[] GetInstalledPrinters()
    {
        if (OperatingSystem.IsWindows()) return WindowsPrinterService.GetInstalledPrinters();
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) return GetCupsPrinters();
        return [];
    }

    // Returns settings choices with the portable system-default destination first.
    public static string[] GetPrinterChoices() => [DefaultPrinter, .. GetInstalledPrinters()];

    // Returns the current default printer reported by Windows or CUPS.
    public static string? GetDefaultPrinter()
    {
        if (OperatingSystem.IsWindows()) return WindowsPrinterService.GetDefaultPrinter();
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return null;
        var result = RunCommand("lpstat", ["-d"]);
        if (result.ExitCode != 0) return null;
        const string prefix = "system default destination:";
        var line = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line?[prefix.Length..].Trim();
    }

    // Normalizes saved default labels from older versions and validates explicit destinations.
    public static string? ResolvePrinter(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName) ||
            printerName.Equals(DefaultPrinter, StringComparison.OrdinalIgnoreCase) ||
            printerName.Equals(LegacyWindowsDefaultPrinter, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!GetInstalledPrinters().Contains(printerName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Printer '{printerName}' is not installed or is unavailable.");
        return printerName;
    }

    // Selects a Windows printer for Chromium; CUPS destinations are supplied when the job is submitted.
    public static IDisposable UseWindowsPrinter(string? printerName)
    {
        if (!OperatingSystem.IsWindows()) return EmptyScope.Instance;
        return WindowsPrinterService.UsePrinter(printerName);
    }

    // Sends a rendered spool file to the selected macOS or Linux CUPS destination.
    public static async Task SubmitCupsJobAsync(string path, string? printerName, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("CUPS printing is available only on macOS and Linux.");

        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(printerName))
        {
            arguments.Add("-d");
            arguments.Add(printerName);
        }
        arguments.Add(path);

        var result = await RunCommandAsync("lp", arguments, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"CUPS could not submit the print job: {result.Error.Trim()}");
    }

    // Parses one destination name per line as returned by `lpstat -e`.
    internal static string[] ParseCupsDestinations(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    // Queries CUPS for available local and network printer destinations.
    private static string[] GetCupsPrinters()
    {
        var result = RunCommand("lpstat", ["-e"]);
        return result.ExitCode == 0 ? ParseCupsDestinations(result.Output) : [];
    }

    // Runs a short synchronous discovery command without opening a terminal window.
    private static CommandResult RunCommand(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            using var process = StartProcess(fileName, arguments);
            if (!process.WaitForExit(3000))
            {
                process.Kill(true);
                return new CommandResult(-1, "", "The printer command timed out.");
            }
            return new CommandResult(process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new CommandResult(-1, "", ex.Message);
        }
    }

    // Runs a CUPS submission command asynchronously and captures diagnostic output.
    private static async Task<CommandResult> RunCommandAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = StartProcess(fileName, arguments);
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new CommandResult(process.ExitCode, await output, await error);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("CUPS printing tools are not installed. Install and enable CUPS so the 'lp' and 'lpstat' commands are available.", ex);
        }
    }

    // Starts a native print command with argument-list escaping handled by ProcessStartInfo.
    private static Process StartProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }
}
