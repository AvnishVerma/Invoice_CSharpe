using System.Globalization;
using LedgerNest.Domain;
using SkiaSharp;

namespace LedgerNest.Desktop;

internal static class DocumentPdf
{
    internal sealed record Business(string Name, string Address, string Phone, string Email, string TaxId, string Logo, string Note);

    public static byte[] Create(Invoice invoice, InvoiceItem[] items, Business business, string pageSize, bool landscape)
    {
        var (width, height) = pageSize switch
        {
            "A5" => (420f, 595f), "A6" => (298f, 420f),
            "Thermal 80mm" => (227f, 700f), "Thermal 58mm" => (164f, 600f), _ => (595f, 842f)
        };
        if (landscape && !pageSize.StartsWith("Thermal", StringComparison.Ordinal)) (width, height) = (height, width);
        var narrow = width < 300;
        var margin = narrow ? 12f : 32f;
        var usable = width - 2 * margin;
        var size = narrow ? 8f : 10f;
        using var regularStream = typeof(DocumentPdf).Assembly.GetManifestResourceStream("LedgerNest.Pdf.Regular");
        using var boldStream = typeof(DocumentPdf).Assembly.GetManifestResourceStream("LedgerNest.Pdf.Bold");
        using var regular = SKTypeface.FromStream(regularStream);
        using var bold = SKTypeface.FromStream(boldStream);
        using var paint = new SKPaint { IsAntialias = true, Typeface = regular, TextSize = size };
        using var output = new MemoryStream();
        using var pdf = SKDocument.CreatePdf(output);
        SKCanvas canvas = null!;
        var page = 0; float y = 0;
        var ink = SKColor.Parse("#172B3A"); var muted = SKColor.Parse("#52616B"); var accent = SKColor.Parse("#0F766E");
        void Text(string text, float x, float baseline, float fontSize, bool strong = false, SKColor? color = null, bool right = false)
        {
            paint.Typeface = strong ? bold : regular; paint.TextSize = fontSize; paint.Color = color ?? ink;
            canvas.DrawText(text, right ? x - paint.MeasureText(text) : x, baseline, paint);
        }
        void Rule(float baseline)
        {
            paint.Color = SKColor.Parse("#DDE5E7"); paint.StrokeWidth = 0.7f;
            canvas.DrawLine(margin, baseline, width - margin, baseline, paint);
        }
        void FinishPage()
        {
            Rule(height - 29);
            Text($"{Branding.Name} · {invoice.Type}", margin, height - 16, 7, color: muted);
            Text($"Page {page}", width - margin, height - 16, 7, color: muted, right: true);
            pdf.EndPage();
        }
        void NewPage()
        {
            if (page > 0) FinishPage();
            canvas = pdf.BeginPage(width, height); page++; canvas.Clear(SKColors.White);
            paint.Color = accent; canvas.DrawRect(0, 0, width, 6, paint);
            Text(invoice.Type.ToUpperInvariant(), margin, margin + 20, narrow ? 17 : 25, true, accent);
            if (invoice.Snapshot?.HideInvoiceNumber != true)
                Text("#" + invoice.InvoiceNumber, margin, margin + 36, size, color: muted);
            y = margin + 53;
        }
        void Ensure(float space) { if (y + space > height - 45) NewPage(); }
        // Wrap by measured glyph width, including long identifiers without spaces.
        IEnumerable<string> Wrap(string? text, float maxWidth, float fontSize)
        {
            paint.Typeface = regular; paint.TextSize = fontSize;
            foreach (var paragraph in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var remaining = paragraph;
                while (remaining.Length > 0)
                {
                    var count = remaining.Length;
                    while (count > 1 && paint.MeasureText(remaining[..count]) > maxWidth) count--;
                    if (count < remaining.Length)
                    {
                        var space = remaining.LastIndexOf(' ', count - 1, count);
                        if (space > 0) count = space;
                    }
                    yield return remaining[..count]; remaining = remaining[count..].TrimStart();
                }
            }
        }
        void Paragraph(string? text, bool strong = false)
        {
            foreach (var line in Wrap(text, usable, size).ToArray())
            { Ensure(size + 7); Text(line, margin, y, size, strong); y += size + 5; }
        }
        string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
        NewPage();
        if (!string.IsNullOrWhiteSpace(business.Logo))
        {
            try
            {
                using var logo = business.Logo.StartsWith("base64:", StringComparison.Ordinal)
                    ? SKBitmap.Decode(Convert.FromBase64String(business.Logo[7..])) : SKBitmap.Decode(business.Logo);
                if (logo != null)
                {
                    var scale = Math.Min(usable / logo.Width, 42f / logo.Height);
                    canvas.DrawBitmap(logo, new SKRect(margin, y, margin + logo.Width * scale, y + logo.Height * scale)); y += 52;
                }
            }
            catch (Exception ex) when (ex is IOException or FormatException or ArgumentException) { }
        }
        Paragraph(string.IsNullOrWhiteSpace(business.Name) ? Branding.Name : business.Name, true);
        Paragraph(business.Address); Paragraph(business.Phone); Paragraph(business.Email);
        if (business.TaxId.Length > 0) Paragraph("GST / Tax ID: " + business.TaxId);
        y += 8; Ensure(45); Rule(y); y += 18;
        Paragraph("Date: " + invoice.InvoiceDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
        Paragraph("Status: " + invoice.Status);
        Paragraph("Customer: " + (invoice.Snapshot?.Customer.Name ?? invoice.CustomerName), true);
        Paragraph(invoice.Snapshot?.Customer.Address);
        Paragraph(invoice.Snapshot?.Customer.Phone);
        y += 8;
        void TableHeader()
        {
            Ensure(36); paint.Color = accent; canvas.DrawRect(margin, y, usable, 23, paint);
            Text("ITEM / SERVICE", margin + 6, y + 15, size, true, SKColors.White);
            if (!narrow)
            {
                Text("QTY", margin + usable * .63f, y + 15, size, true, SKColors.White, true);
                Text("RATE", margin + usable * .81f, y + 15, size, true, SKColors.White, true);
                Text("TAX %", width - margin - 6, y + 15, size, true, SKColors.White, true);
            }
            y += 38;
        }
        TableHeader();
        foreach (var item in items)
        {
            var description = Wrap(item.Description, narrow ? usable - 12 : usable * .51f, size).DefaultIfEmpty("Item").ToArray();
            if (y + size + 26 > height - 45) { NewPage(); TableHeader(); }
            Text(description[0], margin + 6, y, size, true);
            if (!narrow)
            {
                Text(item.Quantity.ToString("0.###", CultureInfo.InvariantCulture), margin + usable * .63f, y, size, right: true);
                Text(Money(item.UnitPrice), margin + usable * .81f, y, size, right: true);
                Text(item.TaxRate.ToString("0.##", CultureInfo.InvariantCulture), width - margin - 6, y, size, right: true);
            }
            y += size + 5;
            foreach (var line in description.Skip(1))
            {
                if (y + size + 6 > height - 45) { NewPage(); TableHeader(); }
                Text(line, margin + 6, y, size); y += size + 5;
            }
            if (narrow) Paragraph($"Qty {item.Quantity:0.###} × {Money(item.UnitPrice)} · Tax {item.TaxRate:0.##}%");
            if (item.Discount != 0) Paragraph("Item discount: " + Money(item.DiscountPerUnit ? item.Discount * item.Quantity : item.Discount));
            if (item.ExtraCost != 0) Paragraph("Item extra cost: " + Money(item.ExtraCost));
            Ensure(10); Rule(y); y += 16;
        }
        Ensure(135); y += 6;
        var currency = invoice.Snapshot?.Currency?.Split('—')[0].Trim() ?? "";
        Paragraph("AMOUNTS" + (currency.Length > 0 ? " · " + currency : ""), true);
        void Total(string label, decimal amount, bool strong = false)
        {
            Ensure(24); Text(label, margin, y, size, strong); Text(Money(amount), width - margin, y, size, strong, right: true); y += 21;
        }
        Total("Subtotal", invoice.SubTotal); Total("Tax", invoice.TaxTotal);
        if (invoice.DiscountTotal != 0) Total("Discount", invoice.DiscountTotal);
        Total("Total", invoice.GrandTotal, true); Total("Paid", invoice.PaidAmount);
        Total("Balance due", invoice.GrandTotal - invoice.PaidAmount, true);
        y += 8; Paragraph(invoice.Snapshot?.Notes); Paragraph(business.Note);
        FinishPage(); pdf.Close();
        return output.ToArray();
    }
}
