using System.ComponentModel;
using System.Drawing.Printing;
using System.Runtime.Versioning;

namespace LedgerNest.Desktop;

// Discovers Windows printers without changing the user's default printer.
internal static class WindowsPrinterService
{
    // Returns installed Windows printer names with the current default printer first.
    public static string[] GetInstalledPrinters()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return [];
        return GetWindowsPrinters();
    }

    // Returns the active Windows default printer when the platform supports printing.
    public static string? GetDefaultPrinter()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return null;
        try { return new PrinterSettings().PrinterName; }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception) { return null; }
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
            return names;
        }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception)
        {
            return [];
        }
    }

}
