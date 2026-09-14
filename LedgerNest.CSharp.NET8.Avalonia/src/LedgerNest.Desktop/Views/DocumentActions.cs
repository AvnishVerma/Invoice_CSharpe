using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private void ShowDocumentPreview(UiRecord document)
    {
        var bytes = TryExportDocumentPdf(document);
        if (bytes == null) return;
        ShowOverlay("PDF Preview", Ui.Stack(14,
            Ui.Text($"Document: {document.Name}", 16, true),
            Ui.Text("PDF preview is ready. Use Download or Print to create an output file.")),
            Ui.Wrap(Ui.Button("Download", async () => await DownloadDocumentPdf(document)), Ui.Button("Print", async () => await PrintDocumentPdf(document)), Ui.Button("Close", CloseOverlay, true)),
            width: 520);
    }

    private async Task DownloadDocumentPdf(UiRecord document)
    {
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            if (OperatingSystem.IsMacOS())
            {
                var path = FilePickerHelpers.MacDownloadsPdfPath(document.Name);
                await File.WriteAllBytesAsync(path, bytes);
                Model.Status = $"Saved PDF to {path}";
                return;
            }
            var file = await StorageProvider.SaveFilePickerAsync(FilePickerHelpers.PdfSaveOptions($"Save {document.Name} PDF", document.Name));
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await LedgerNest.Infrastructure.BackupStreamWriter.WriteAsync(stream, bytes);
            Model.Status = $"Saved {file.Name}.";
        }
        catch (Exception ex)
        {
            NotifyError("Could not save PDF. The error has been logged.", ex, $"Saving invoice PDF {document.Name}");
        }
    }

    internal async Task PrintDocumentPdf(UiRecord document)
    {
        string? path = null;
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            path = Path.Combine(Path.GetTempPath(), $"ledgernest-{FilePickerHelpers.SanitizeFileName(document.Name)}-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            await SendPdfToPrinter(path);
            Model.Status = $"Sent {document.Name} to printer.";
        }
        catch (Exception ex)
        {
            NotifyError(path == null ? "Could not create print PDF. The error has been logged." : $"Could not send PDF to printer. Saved print file: {path}. The error has been logged.", ex, $"Printing invoice PDF {document.Name}");
        }
    }

    private static async Task SendPdfToPrinter(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var process = Process.Start(new ProcessStartInfo(path) { Verb = "print", UseShellExecute = true, CreateNoWindow = true });
            if (process == null) throw new InvalidOperationException("The system print command could not be started.");
            return;
        }

        var command = OperatingSystem.IsMacOS() ? "lp" : File.Exists("/usr/bin/lp") || File.Exists("/bin/lp") ? "lp" : "lpr";
        using var print = Process.Start(new ProcessStartInfo(command, path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });
        if (print == null) throw new InvalidOperationException("The system print command could not be started.");
        await print.WaitForExitAsync();
        if (print.ExitCode != 0)
        {
            var error = await print.StandardError.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(error)) error = await print.StandardOutput.ReadToEndAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Print command exited with code {print.ExitCode}." : error.Trim());
        }
    }

    private void DeleteDocumentFromDashboard(UiRecord document)
    {
        Confirm("Confirm Delete", $"Move {document.Name} to trash?", () =>
        {
            Model.SetDocumentTrash(document, true);
            page.Content = Dashboard();
        });
    }

    private byte[]? TryExportDocumentPdf(UiRecord document)
    {
        try
        {
            return Model.ExportDocumentPdf(document);
        }
        catch (Exception ex)
        {
            NotifyError($"Could not export {document.Name}. The error has been logged.", ex, $"Exporting invoice PDF {document.Name}");
            return null;
        }
    }

    private void NotifyError(string message, Exception exception, string context)
    {
        AppErrorLog.Write(exception, context);
        Model.Status = $"{message} Log: {AppErrorLog.Path}";
    }
}
