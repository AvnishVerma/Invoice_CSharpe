using Avalonia.Platform.Storage;

namespace LedgerNest.Desktop;

internal static class FilePickerHelpers
{
    public static FilePickerSaveOptions PdfSaveOptions(string title, string suggestedName) => new()
    {
        Title = title,
        SuggestedFileName = EnsureExtension(SanitizeFileName(suggestedName), ".pdf"),
        DefaultExtension = "pdf",
        FileTypeChoices = [new FilePickerFileType("PDF files") { Patterns = ["*.pdf"] }]
    };

    private static string EnsureExtension(string name, string extension) =>
        name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : name + extension;

    private static string SanitizedFallback => "ledgernest-document";

    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Select(c => invalid.Contains(c) || c is '#' or '[' or ']' or ':' ? '-' : c).ToArray());
        cleaned = string.Join("-", cleaned.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? SanitizedFallback : cleaned;
    }
}
