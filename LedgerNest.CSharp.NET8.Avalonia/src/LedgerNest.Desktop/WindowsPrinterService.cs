using System.ComponentModel;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LedgerNest.Desktop;

// Discovers Windows printers and temporarily selects one for Chromium kiosk printing.
internal static class WindowsPrinterService
{
    public const string DefaultPrinter = "Windows default printer";

    // Returns installed Windows printer names with the current default printer first.
    public static string[] GetInstalledPrinters()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return [DefaultPrinter];
        return GetWindowsPrinters();
    }

    // Reads the installed printer collection on supported Windows versions.
    [SupportedOSPlatform("windows6.1")]
    private static string[] GetWindowsPrinters()
    {
        try
        {
            var current = new PrinterSettings().PrinterName;
            var names = PrinterSettings.InstalledPrinters.Cast<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name.Equals(current, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return names.Length == 0 ? [DefaultPrinter] : [DefaultPrinter, .. names];
        }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception)
        {
            return [DefaultPrinter];
        }
    }

    // Makes the selected printer the Windows default for one direct-print operation.
    public static IDisposable UsePrinter(string printerName)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1) || string.IsNullOrWhiteSpace(printerName) || printerName == DefaultPrinter)
            return EmptyScope.Instance;

        return UseWindowsPrinter(printerName);
    }

    // Changes the Windows default printer until the returned scope is disposed.
    [SupportedOSPlatform("windows6.1")]
    private static IDisposable UseWindowsPrinter(string printerName)
    {
        var installed = GetInstalledPrinters();
        if (!installed.Contains(printerName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidPrinterException(new PrinterSettings { PrinterName = printerName });

        var previous = new PrinterSettings().PrinterName;
        if (printerName.Equals(previous, StringComparison.OrdinalIgnoreCase)) return EmptyScope.Instance;
        if (!SetDefaultPrinter(printerName)) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not select printer '{printerName}'.");
        return new RestoreDefaultPrinterScope(previous);
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultPrinter(string printerName);

    private sealed class RestoreDefaultPrinterScope(string printerName) : IDisposable
    {
        private bool disposed;

        // Restores the user's previous Windows default printer after Chromium has handed off the job.
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!string.IsNullOrWhiteSpace(printerName)) SetDefaultPrinter(printerName);
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }
}
