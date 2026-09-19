namespace LedgerNest.Desktop;

// Holds portable printer labels and CUPS parsing shared by platform services and settings.
internal static class PlatformPrinterService
{
    public const string DefaultPrinter = "System default printer";
    private const string LegacyWindowsDefaultPrinter = "Windows default printer";

    // Normalizes saved default labels from older versions and validates explicit destinations.
    public static string? NormalizePrinterName(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName) ||
            printerName.Equals(DefaultPrinter, StringComparison.OrdinalIgnoreCase) ||
            printerName.Equals(LegacyWindowsDefaultPrinter, StringComparison.OrdinalIgnoreCase))
            return null;

        return printerName;
    }

    // Parses one destination name per line as returned by `lpstat -e`.
    internal static string[] ParseCupsDestinations(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

}
