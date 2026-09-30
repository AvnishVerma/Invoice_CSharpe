using LedgerNest.Domain;
using SkiaSharp;

namespace LedgerNest.Desktop;

public static class BarcodeLabelPdf
{
    private static readonly Dictionary<char, string> Patterns = new()
    {
        ['0']="nnnwwnwnn",['1']="wnnwnnnnw",['2']="nnwwnnnnw",['3']="wnwwnnnnn",['4']="nnnwwnnnw",['5']="wnnwwnnnn",['6']="nnwwwnnnn",['7']="nnnwnnwnw",['8']="wnnwnnwnn",['9']="nnwwnnwnn",
        ['A']="wnnnnwnnw",['B']="nnwnnwnnw",['C']="wnwnnwnnn",['D']="nnnnwwnnw",['E']="wnnnwwnnn",['F']="nnwnwwnnn",['G']="nnnnnwwnw",['H']="wnnnnwwnn",['I']="nnwnnwwnn",['J']="nnnnwwwnn",
        ['K']="wnnnnnnww",['L']="nnwnnnnww",['M']="wnwnnnnwn",['N']="nnnnwnnww",['O']="wnnnwnnwn",['P']="nnwnwnnwn",['Q']="nnnnnnwww",['R']="wnnnnnwwn",['S']="nnwnnnwwn",['T']="nnnnwnwwn",
        ['U']="wwnnnnnnw",['V']="nwwnnnnnw",['W']="wwwnnnnnn",['X']="nwnnwnnnw",['Y']="wwnnwnnnn",['Z']="nwwnwnnnn",['-']="nwnnnnwnw",['.']="wwnnnnwnn",[' ']="nwwnnnwnn",['*']="nwnnwnwnn",['$']="nwnwnwnnn",['/']="nwnwnnnwn",['+']="nwnnnwnwn",['%']="nnnwnwnwn"
    };

    public static byte[] Create(IReadOnlyList<BarcodeLabelData> labels, string currency)
    {
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);
        const float pageWidth = 595, pageHeight = 842, margin = 24, gap = 8;
        const int columns = 3, rows = 8;
        var width = (pageWidth - margin * 2 - gap * (columns - 1)) / columns;
        var height = (pageHeight - margin * 2 - gap * (rows - 1)) / rows;
        using var text = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var regularTypeface = SKTypeface.Default;
        using var boldTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var regular8 = new SKFont(regularTypeface, 8);
        using var bold9 = new SKFont(boldTypeface, 9);
        using var bold10 = new SKFont(boldTypeface, 10);
        for (var start = 0; start < labels.Count; start += columns * rows)
        {
            using var canvas = document.BeginPage(pageWidth, pageHeight);
            canvas.Clear(SKColors.White);
            foreach (var (label, offset) in labels.Skip(start).Take(columns * rows).Select((item, index) => (item, index)))
            {
                var column = offset % columns; var row = offset / columns;
                var x = margin + column * (width + gap); var y = margin + row * (height + gap);
                using var outline = new SKPaint { Color = new SKColor(220, 225, 232), Style = SKPaintStyle.Stroke, StrokeWidth = .7f };
                canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 5, 5, outline);
                canvas.DrawText(Trim(label.ProductName, 25), x + 7, y + 16, SKTextAlign.Left, bold10, text);
                DrawCode39(canvas, label.Code, x + 7, y + 24, width - 14, 42);
                canvas.DrawText(label.Code, x + 7, y + 75, SKTextAlign.Left, regular8, text);
                canvas.DrawText($"{currency} {label.Price:0.00}", x + 7, y + 89, SKTextAlign.Left, bold9, text);
            }
            document.EndPage();
        }
        document.Close();
        return stream.ToArray();
    }

    private static void DrawCode39(SKCanvas canvas, string value, float x, float y, float maxWidth, float height)
    {
        var encoded = $"*{BarcodeRules.NormalizeCode39(value)}*";
        var units = encoded.Sum(character => Patterns[character].Sum(part => part == 'w' ? 3 : 1) + 1);
        var narrow = maxWidth / units;
        using var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        var position = x;
        foreach (var character in encoded)
        {
            var pattern = Patterns[character];
            for (var index = 0; index < pattern.Length; index++)
            {
                var segment = narrow * (pattern[index] == 'w' ? 3 : 1);
                if (index % 2 == 0) canvas.DrawRect(position, y, segment, height, paint);
                position += segment;
            }
            position += narrow;
        }
    }

    private static string Trim(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";
}
