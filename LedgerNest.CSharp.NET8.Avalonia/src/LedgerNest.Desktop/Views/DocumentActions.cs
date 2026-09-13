using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

    private async Task PrintDocumentPdf(UiRecord document)
    {
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            var path = Path.Combine(Path.GetTempPath(), $"ledgernest-{FilePickerHelpers.SanitizeFileName(document.Name)}-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            Model.Status = $"Print-ready PDF created: {path}";
        }
        catch (Exception ex)
        {
            NotifyError("Could not create print PDF. The error has been logged.", ex, $"Creating print PDF {document.Name}");
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
