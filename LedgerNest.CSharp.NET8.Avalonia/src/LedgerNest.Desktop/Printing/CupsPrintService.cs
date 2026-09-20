using System.ComponentModel;
using System.Diagnostics;

namespace LedgerNest.Desktop.Printing;

// Discovers printers and submits PDF jobs through the native CUPS client on macOS and Linux.
public class CupsPrintService(bool macOS) : IPrintService
{
    public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("lpstat", ["-e"], cancellationToken);
        if (result.ExitCode != 0) throw CupsUnavailable(result.Error);
        var defaultPrinter = await GetDefaultPrinterAsync(cancellationToken);
        return PlatformPrinterService.ParseCupsDestinations(result.Output)
            .Select(name => new PrinterInfo(name, name.Equals(defaultPrinter?.Name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public async Task<PrinterInfo?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("lpstat", ["-d"], cancellationToken);
        if (result.ExitCode != 0) return null;
        const string prefix = "system default destination:";
        var line = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line == null ? null : new PrinterInfo(line[prefix.Length..].Trim(), true);
    }

    public async Task PrintPdfAsync(string pdfFilePath, string? printerName = null, PrintOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfFilePath)) throw new FileNotFoundException("The generated PDF file was not found.", pdfFilePath);
        options ??= new PrintOptions();
        if (options.ShowPrintDialog) throw new NotSupportedException($"A native print dialog is not available through the {(macOS ? "macOS" : "Linux")} CUPS command line. Select a printer in LedgerNest before printing.");
        var printers = await GetPrintersAsync(cancellationToken);
        var resolved = PlatformPrinterService.NormalizePrinterName(printerName);
        if (resolved != null && !printers.Any(printer => printer.Name.Equals(resolved, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Printer '{printerName}' was not found.");
        if (resolved != null && PrinterClassifier.IsFilePrinter(resolved))
            throw new InvalidOperationException($"Printer '{resolved}' saves to a file. Select a physical printer for direct printing.");
        if (resolved == null)
        {
            var defaultPrinter = await GetDefaultPrinterAsync(cancellationToken);
            resolved = PrinterClassifier.IsFilePrinter(defaultPrinter?.Name)
                ? printers.FirstOrDefault(item => !PrinterClassifier.IsFilePrinter(item.Name))?.Name
                : defaultPrinter?.Name;
            if (string.IsNullOrWhiteSpace(resolved))
                resolved = printers.FirstOrDefault(item => !PrinterClassifier.IsFilePrinter(item.Name))?.Name;
            if (string.IsNullOrWhiteSpace(resolved))
                throw new InvalidOperationException("No physical printer is available. Select or install a physical printer before printing.");
        }
        var args = new List<string>();
        args.Add("-d"); args.Add(resolved);
        if (options.Copies is > 1) { args.Add("-n"); args.Add(options.Copies.Value.ToString()); }
        if (options.Landscape) { args.Add("-o"); args.Add("landscape"); }
        if (!string.IsNullOrWhiteSpace(options.PaperSize)) { args.Add("-o"); args.Add("media=" + options.PaperSize); }
        if (options.Collate) { args.Add("-o"); args.Add("Collate=True"); }
        args.Add(pdfFilePath);
        var result = await RunAsync("lp", args, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException($"CUPS could not submit the print job: {result.Error.Trim()}");
    }

    private static async Task<CommandResult> RunAsync(string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start '{command}'.");
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new CommandResult(process.ExitCode, await output, await error);
        }
        catch (Win32Exception ex) { throw CupsUnavailable(ex.Message, ex); }
    }

    private static InvalidOperationException CupsUnavailable(string detail, Exception? inner = null) =>
        new($"CUPS printing service is not available. Install and enable CUPS with the 'lp' and 'lpstat' commands. {detail}".Trim(), inner);

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}

// Uses the macOS CUPS client without launching Preview or another viewer.
public sealed class MacPrintService() : CupsPrintService(true);

// Uses the Linux CUPS client without launching a PDF viewer.
public sealed class LinuxPrintService() : CupsPrintService(false);
