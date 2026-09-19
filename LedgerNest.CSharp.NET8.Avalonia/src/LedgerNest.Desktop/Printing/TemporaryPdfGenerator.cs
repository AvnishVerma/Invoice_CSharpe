namespace LedgerNest.Desktop.Printing;

// Stores an already-rendered document in a unique OS temporary PDF for native printing.
public sealed class TemporaryPdfGenerator : IPdfGenerator
{
    public async Task<string> GenerateAsync(ReadOnlyMemory<byte> pdfBytes, string documentName, CancellationToken cancellationToken = default)
    {
        if (pdfBytes.IsEmpty) throw new InvalidDataException("PDF generation produced an empty document.");
        var safeName = FilePickerHelpers.SanitizeFileName(documentName);
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-{safeName}-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, pdfBytes.ToArray(), cancellationToken);
        return path;
    }
}
