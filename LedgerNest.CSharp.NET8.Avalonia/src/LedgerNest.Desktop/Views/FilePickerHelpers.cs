using Avalonia.Platform.Storage;

namespace LedgerNest.Desktop;

internal static class FilePickerHelpers
{
    // Performs the pdf save options action for this screen or workflow.
    public static FilePickerSaveOptions PdfSaveOptions(string title, string suggestedName) => new()
    {
        Title = title,
        SuggestedFileName = EnsureExtension(SanitizeFileName(suggestedName), ".pdf"),
        DefaultExtension = "pdf",
        FileTypeChoices = [new FilePickerFileType("PDF files") { Patterns = ["*.pdf"] }]
    };

    // Performs the ensure extension action for this screen or workflow.
    private static string EnsureExtension(string name, string extension) =>
        name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : name + extension;

    private static string SanitizedFallback => "ledgernest-document";

    // Performs the mac downloads pdf path action for this screen or workflow.
    public static string MacDownloadsPdfPath(string suggestedName)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(downloads);
        var baseName = Path.GetFileNameWithoutExtension(SanitizeFileName(suggestedName));
        var path = Path.Combine(downloads, EnsureExtension(baseName, ".pdf"));
        if (!File.Exists(path)) return path;
        return Path.Combine(downloads, $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
    }

    // Performs the sanitize file name action for this screen or workflow.
    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Select(c => invalid.Contains(c) || c is '#' or '[' or ']' or ':' ? '-' : c).ToArray());
        cleaned = string.Join("-", cleaned.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? SanitizedFallback : cleaned;
    }
}
