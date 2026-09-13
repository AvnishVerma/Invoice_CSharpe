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
        var bytes = TryExportDocumentPdf(document);
        if (bytes == null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = $"Save {document.Name} PDF",
            SuggestedFileName = $"{document.Name}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = [new FilePickerFileType("PDF files") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }]
        });
        if (file == null) return;
        await using var stream = await file.OpenWriteAsync();
        await LedgerNest.Infrastructure.BackupStreamWriter.WriteAsync(stream, bytes);
        Model.Status = $"Saved {file.Name}.";
    }

    private async Task PrintDocumentPdf(UiRecord document)
    {
        var bytes = TryExportDocumentPdf(document);
        if (bytes == null) return;
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-{document.Name}-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, bytes);
        Model.Status = $"Print-ready PDF created: {path}";
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
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            Model.Status = $"Could not export {document.Name}: {ex.Message}";
            return null;
        }
    }
}
