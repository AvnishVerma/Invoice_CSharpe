namespace LedgerNest.Application;

public static class PdfFontSizeRules
{
    public static readonly string[] Presets = ["Small", "Medium", "Large", "X-Large"];
    public static readonly string[] SectionPresets = ["Inherit", .. Presets];

    public static float Resolve(string? preset, string? inherited = "Medium") =>
        (string.Equals(preset, "Inherit", StringComparison.OrdinalIgnoreCase) ? inherited : preset)?.Trim().ToLowerInvariant() switch
        {
            "small" => .9f, "large" => 1.2f, "x-large" or "xlarge" => 1.4f, _ => 1f
        };
}
