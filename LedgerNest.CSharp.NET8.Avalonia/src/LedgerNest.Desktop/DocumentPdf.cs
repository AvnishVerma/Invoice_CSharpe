using System.Globalization;
using LedgerNest.Domain;
using SkiaSharp;

namespace LedgerNest.Desktop;

internal static class DocumentPdf
{
    internal sealed record Business(string Name, string Address, string Phone, string Email, string TaxId, string Logo, string Note);
    internal sealed record PdfExportOptions(
        string DateFormat,
        string TimeFormat,
        bool ShowTime,
        string QuantityLabel,
        bool ShowDescription,
        bool DescriptionOnNewLine,
        bool ShowCustomerBusinessName,
        bool ShowCustomerAddress,
        bool ShowCustomerPhone,
        bool ShowCustomerEmail,
        bool ShowCustomerGstin,
        bool ShowSlNo,
        bool ShowItemName,
        bool ShowQuantity,
        bool ShowPrice,
        bool ShowTax,
        bool ShowDiscount,
        bool ShowTotal,
        bool ShowTotalQuantity)
    {
        public string InvoicePrefix { get; init; } = "";
        public bool LeadingZeros { get; init; } = true;
        public string AdditionalInformation { get; init; } = "";
        public string LogoPosition { get; init; } = "Left";
        public float LogoSize { get; init; } = 90;
        public string SignatureImage { get; init; } = "";
        public string SignaturePosition { get; init; } = "Right";
        public float SignatureSize { get; init; } = 50;
        public string WatermarkImage { get; init; } = "";
        public float WatermarkOpacity { get; init; } = .15f;
        public bool ShowGst { get; init; } = true;
        public bool ShowHsn { get; init; } = true;
        public bool ShowRoundOff { get; init; }
        public bool ShowAliasName { get; init; }
        public bool ShowProductServiceTag { get; init; }
        public string[] MetadataColumns { get; init; } = [];
        public decimal PreviousBalance { get; init; }
    }

    internal static string DisplayNumber(Invoice invoice, PdfExportOptions options)
    {
        if (invoice.Snapshot?.HideInvoiceNumber == true) return invoice.Snapshot.CustomInvoiceNumber.Trim();
        var number = invoice.InvoiceNumber;
        if (long.TryParse(number, out var numeric)) number = numeric.ToString(options.LeadingZeros ? "D8" : "0", CultureInfo.InvariantCulture);
        return (string.IsNullOrWhiteSpace(options.InvoicePrefix) ? "" : options.InvoicePrefix.Trim() + "-") + number;
    }

    // Performs the create action for this screen or workflow.
    public static byte[] Create(Invoice invoice, InvoiceItem[] items, Business business, string pageSize, bool landscape, string template = "Classic", string themeColor = "#0F766E", PdfExportOptions? options = null)
    {
        options ??= new PdfExportOptions("dd MMM yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true);
        template = string.IsNullOrWhiteSpace(template) ? "Classic" : template.Trim();
        var useThermalTemplate = template.Equals("Thermal", StringComparison.OrdinalIgnoreCase);
        var (width, height) = (useThermalTemplate && !pageSize.StartsWith("Thermal", StringComparison.Ordinal))
            ? (227f, 700f)
            : pageSize switch
            {
                "A5" => (420f, 595f), "A6" => (298f, 420f),
                "Thermal 80mm" => (227f, 700f), "Thermal 58mm" => (164f, 600f), _ => (595f, 842f)
            };
        if (landscape && !pageSize.StartsWith("Thermal", StringComparison.Ordinal) && !useThermalTemplate) (width, height) = (height, width);
        var narrow = width < 300 || useThermalTemplate;
        var compact = template.Equals("Compact", StringComparison.OrdinalIgnoreCase);
        var minimal = template.Equals("Minimal", StringComparison.OrdinalIgnoreCase);
        var modern = template.Equals("Modern", StringComparison.OrdinalIgnoreCase);
        var executive = template.Equals("Executive", StringComparison.OrdinalIgnoreCase);
        var grid = template.Equals("Grid Classic", StringComparison.OrdinalIgnoreCase);
        var title = string.IsNullOrWhiteSpace(invoice.Snapshot?.DocumentTitle) ? invoice.Type : invoice.Snapshot.DocumentTitle;
        var displayNumber = DisplayNumber(invoice, options);
        var quantityLabel = string.IsNullOrWhiteSpace(options.QuantityLabel) ? "Qty" : options.QuantityLabel;
        var thermal = useThermalTemplate || pageSize.StartsWith("Thermal", StringComparison.Ordinal);
        static SKBitmap? DecodeImage(string value)
        {
            try { return value.StartsWith("base64:", StringComparison.Ordinal) ? SKBitmap.Decode(Convert.FromBase64String(value[7..])) : File.Exists(value) ? SKBitmap.Decode(value) : null; }
            catch (Exception ex) when (ex is IOException or FormatException or ArgumentException) { return null; }
        }
        using var watermark = thermal ? null : DecodeImage(options.WatermarkImage);
        var margin = narrow ? 12f : compact ? 24f : 32f;
        var usable = width - 2 * margin;
        var size = narrow ? 8f : compact ? 8.5f : 10f;
        using var regularStream = typeof(DocumentPdf).Assembly.GetManifestResourceStream("LedgerNest.Pdf.Regular");
        using var boldStream = typeof(DocumentPdf).Assembly.GetManifestResourceStream("LedgerNest.Pdf.Bold");
        using var regular = SKTypeface.FromStream(regularStream);
        using var bold = SKTypeface.FromStream(boldStream);
        using var paint = new SKPaint { IsAntialias = true };
        using var output = new MemoryStream();
        using var pdf = SKDocument.CreatePdf(output);
        SKCanvas canvas = null!;
        var page = 0; float y = 0;
        var ink = SKColor.Parse("#172B3A"); var muted = SKColor.Parse("#52616B"); var ruleColor = SKColor.Parse("#DDE5E7"); var soft = SKColor.Parse("#F7F2FA"); var accent = ParseColor(themeColor, "#0F766E");
        // Performs the parse color action for this screen or workflow.
        static SKColor ParseColor(string value, string fallback)
        {
            try { return SKColor.Parse(string.IsNullOrWhiteSpace(value) ? fallback : value); }
            catch (ArgumentException) { return SKColor.Parse(fallback); }
        }
        SKColor WithAlpha(SKColor color, byte alpha) => new(color.Red, color.Green, color.Blue, alpha);
        void Text(string text, float x, float baseline, float fontSize, bool strong = false, SKColor? color = null, bool right = false)
        {
            using var font = new SKFont(strong ? bold : regular, fontSize);
            paint.Color = color ?? ink;
            canvas.DrawText(text, right ? x - font.MeasureText(text) : x, baseline, SKTextAlign.Left, font, paint);
        }
        void Rule(float baseline)
        {
            paint.Color = ruleColor; paint.StrokeWidth = grid ? 1f : 0.7f;
            canvas.DrawLine(margin, baseline, width - margin, baseline, paint);
        }
        void FinishPage()
        {
            if (minimal) { paint.Color = accent; canvas.DrawRect(margin, height - 31, usable, 1, paint); }
            else Rule(height - 29);
            Text($"{Branding.Name} · {invoice.Type} · {template}", margin, height - 16, 7, color: muted);
            Text($"Page {page}", width - margin, height - 16, 7, color: muted, right: true);
            pdf.EndPage();
        }
        void NewPage()
        {
            if (page > 0) FinishPage();
            canvas = pdf.BeginPage(width, height); page++; canvas.Clear(SKColors.White);
            if (watermark != null)
            {
                var scale = Math.Min(width * .65f / watermark.Width, height * .65f / watermark.Height);
                var w = watermark.Width * scale; var h = watermark.Height * scale;
                using var watermarkPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(Math.Clamp(options.WatermarkOpacity, 0, 1) * 255)), IsAntialias = true };
                canvas.DrawBitmap(watermark, new SKRect((width - w) / 2, (height - h) / 2, (width + w) / 2, (height + h) / 2), watermarkPaint);
            }
            if (modern)
            {
                paint.Color = accent; canvas.DrawRect(0, 0, width, narrow ? 58 : 82, paint);
                Text(title.ToUpperInvariant(), margin, margin + 22, narrow ? 15 : 24, true, SKColors.White);
                if (displayNumber.Length > 0) Text("#" + displayNumber, width - margin, margin + 22, size, true, SKColors.White, true);
                y = narrow ? 76 : 104;
            }
            else if (executive)
            {
                paint.Color = accent; canvas.DrawRect(0, 0, 14, height, paint);
                paint.Color = WithAlpha(accent, 24); canvas.DrawRoundRect(new SKRoundRect(new SKRect(margin, margin - 5, width - margin, margin + 48), 8, 8), paint);
                Text(title.ToUpperInvariant(), margin + 12, margin + 22, narrow ? 16 : 24, true, accent);
                if (displayNumber.Length > 0) Text("#" + displayNumber, width - margin - 12, margin + 22, size, true, muted, true);
                y = margin + 72;
            }
            else if (minimal)
            {
                Text(title.ToUpperInvariant(), margin, margin + 20, narrow ? 16 : 24, true, ink);
                paint.Color = accent; canvas.DrawRect(margin, margin + 30, usable, 1.2f, paint);
                if (displayNumber.Length > 0) Text("#" + displayNumber, width - margin, margin + 20, size, color: muted, right: true);
                y = margin + 54;
            }
            else if (narrow)
            {
                paint.Color = accent; canvas.DrawRect(0, 0, width, 4, paint);
                Text((string.IsNullOrWhiteSpace(business.Name) ? Branding.Name : business.Name).ToUpperInvariant(), width / 2, margin + 12, 10, true, accent, true);
                Text(title.ToUpperInvariant(), width / 2, margin + 28, 14, true, ink, true);
                if (displayNumber.Length > 0) Text("#" + displayNumber, width / 2, margin + 42, size, color: muted, right: true);
                y = margin + 58;
            }
            else
            {
                paint.Color = accent; canvas.DrawRect(0, 0, width, compact ? 4 : 6, paint);
                Text(title.ToUpperInvariant(), margin, margin + 20, compact ? 21 : 25, true, accent);
                if (displayNumber.Length > 0) Text("#" + displayNumber, margin, margin + 36, size, color: muted);
                y = margin + (compact ? 44 : 53);
            }
        }
        void Ensure(float space) { if (y + space > height - 45) NewPage(); }
        // Wrap by measured glyph width, including long identifiers without spaces.
        IEnumerable<string> Wrap(string? text, float maxWidth, float fontSize)
        {
            using var font = new SKFont(regular, fontSize);
            foreach (var paragraph in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var remaining = paragraph;
                while (remaining.Length > 0)
                {
                    var count = remaining.Length;
                    while (count > 1 && font.MeasureText(remaining[..count]) > maxWidth) count--;
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
        string DateTimeText(DateTime value)
        {
            var pattern = string.IsNullOrWhiteSpace(options.DateFormat) ? "dd/MM/yyyy" : options.DateFormat;
            var text = value.ToString(pattern, CultureInfo.InvariantCulture);
            if (!options.ShowTime) return text;
            var timePattern = options.TimeFormat.Contains("12", StringComparison.OrdinalIgnoreCase) ? "hh:mm tt" : "HH:mm";
            return text + " " + value.ToString(timePattern, CultureInfo.InvariantCulture);
        }
        NewPage();
        if (!string.IsNullOrWhiteSpace(business.Logo))
        {
            try
            {
                using var logo = business.Logo.StartsWith("base64:", StringComparison.Ordinal)
                    ? SKBitmap.Decode(Convert.FromBase64String(business.Logo[7..])) : SKBitmap.Decode(business.Logo);
                if (logo != null)
                {
                    var scale = Math.Min(Math.Min(usable, options.LogoSize) / logo.Width, options.LogoSize / logo.Height);
                    var x = options.LogoPosition == "Right" ? width - margin - logo.Width * scale : margin;
                    canvas.DrawBitmap(logo, new SKRect(x, y, x + logo.Width * scale, y + logo.Height * scale)); y += logo.Height * scale + 10;
                }
            }
            catch (Exception ex) when (ex is IOException or FormatException or ArgumentException) { }
        }
        Paragraph(string.IsNullOrWhiteSpace(business.Name) ? Branding.Name : business.Name, true);
        Paragraph(business.Address); Paragraph(business.Phone); Paragraph(business.Email);
        if (options.ShowGst && business.TaxId.Length > 0) Paragraph("GST / Tax ID: " + business.TaxId);
        y += compact ? 3 : 8; Ensure(45); Rule(y); y += narrow ? 12 : 18;
        if (modern || executive)
        {
            paint.Color = WithAlpha(accent, 16); canvas.DrawRoundRect(new SKRoundRect(new SKRect(margin, y - 8, width - margin, y + 54), 6, 6), paint);
        }
        Paragraph("Date: " + DateTimeText(invoice.InvoiceDate));
        Paragraph("Status: " + invoice.Status);
        var customer = invoice.Snapshot?.Customer;
        Paragraph("Customer: " + (customer?.Name ?? invoice.CustomerName), true);
        if (options.ShowCustomerBusinessName && !string.IsNullOrWhiteSpace(customer?.BusinessName)) Paragraph("Business: " + customer.BusinessName);
        if (options.ShowCustomerAddress) Paragraph(customer?.Address);
        if (options.ShowCustomerPhone) Paragraph(customer?.Phone);
        if (!thermal && options.ShowCustomerEmail) Paragraph(customer?.Email);
        if (options.ShowGst && options.ShowCustomerGstin && !string.IsNullOrWhiteSpace(customer?.GstNumber)) Paragraph("GSTIN: " + customer.GstNumber);
        y += 8;
        if (grid && !thermal)
        {
            var customFields = (invoice.Snapshot?.CustomFields ?? []).Where(field => !string.IsNullOrWhiteSpace(field.Value)).ToArray();
            for (var start = 0; start < customFields.Length; start += 3)
            {
                var cells = customFields.Skip(start).Take(3).Select(field => Wrap(field.Label + "\n" + field.Value, usable / 3 - 12, size).ToArray()).ToArray();
                var count = cells.Max(cell => cell.Length);
                for (var line = 0; line < count; line++)
                {
                    Ensure(size + 8);
                    for (var col = 0; col < 3; col++)
                    {
                        paint.Color = ruleColor; paint.Style = SKPaintStyle.Stroke;
                        canvas.DrawRect(margin + col * usable / 3, y, usable / 3, size + 7, paint); paint.Style = SKPaintStyle.Fill;
                        if (col < cells.Length && line < cells[col].Length) Text(cells[col][line], margin + col * usable / 3 + 6, y + size + 2, size, line == 0);
                    }
                    y += size + 7;
                }
            }
            if (customFields.Length > 0) y += 14;
        }
        var metadataColumns = grid && !thermal ? options.MetadataColumns : [];
        var columns = new List<(string Key, string Label, float Weight)>();
        if (options.ShowSlNo) columns.Add(("serial", "#", .45f));
        columns.Add(("item", "Item / Service", 3));
        if (options.ShowGst && options.ShowHsn) columns.Add(("hsn", "HSN/SAC", 1));
        if (options.ShowQuantity) columns.Add(("qty", quantityLabel, .7f));
        columns.Add(("price", options.ShowQuantity ? "Price" : "Rate", 1));
        if (options.ShowTax) columns.Add(("tax", options.ShowGst ? invoice.Snapshot?.IsInterState == true ? "IGST %" : "CGST / SGST %" : "Tax %", 1));
        if (options.ShowDiscount) columns.Add(("discount", "Discount", 1));
        columns.Add(("total", "Total", 1));
        columns.AddRange(metadataColumns.Select(label => ("meta:" + label, label, 1.2f)));
        var tableSize = Math.Min(size, columns.Count > 10 ? 6.5f : 8.5f);
        var widths = columns.Select(column => usable * column.Weight / columns.Sum(c => c.Weight)).ToArray();
        var headers = columns.Select((column, i) => Wrap(column.Label, widths[i] - 8, tableSize).ToArray()).ToArray();
        var headerHeight = headers.Max(lines => lines.Length) * (tableSize + 4) + 8;
        void TableHeader()
        {
            if (!thermal)
            {
                Ensure(headerHeight + 24);
                paint.Color = grid || minimal ? soft : accent;
                canvas.DrawRect(margin, y, usable, headerHeight, paint);
                var x = margin;
                for (var i = 0; i < columns.Count; i++)
                {
                    for (var line = 0; line < headers[i].Length; line++) Text(headers[i][line], x + 4, y + tableSize + 4 + line * (tableSize + 4), tableSize, true, grid || minimal ? ink : SKColors.White);
                    x += widths[i];
                }
                y += headerHeight;
                return;
            }
            Ensure(36);
            if (minimal)
            {
                paint.Color = SKColors.White; canvas.DrawRect(margin, y, usable, 23, paint);
                paint.Color = accent; canvas.DrawRect(margin, y + 22, usable, 1.2f, paint);
            }
            else if (grid)
            {
                paint.Color = soft; canvas.DrawRect(margin, y, usable, 23, paint);
                paint.Color = accent; paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1; canvas.DrawRect(margin, y, usable, 23, paint); paint.Style = SKPaintStyle.Fill;
            }
            else { paint.Color = accent; canvas.DrawRect(margin, y, usable, compact ? 20 : 23, paint); }
            var headerColor = minimal || grid ? ink : SKColors.White;
            Text(options.ShowItemName ? "ITEM / SERVICE" : "DETAILS", margin + 6, y + 15, size, true, headerColor);
            if (!narrow)
            {
                if (options.ShowSlNo) Text("#", margin + usable * .52f, y + 15, size, true, headerColor, true);
                if (options.ShowQuantity) Text(string.IsNullOrWhiteSpace(options.QuantityLabel) ? "QTY" : options.QuantityLabel.ToUpperInvariant(), margin + usable * .63f, y + 15, size, true, headerColor, true);
                if (options.ShowPrice) Text("RATE", margin + usable * .78f, y + 15, size, true, headerColor, true);
                if (options.ShowTax) Text("TAX %", width - margin - 6, y + 15, size, true, headerColor, true);
            }
            y += compact ? 30 : 38;
        }
        TableHeader();
        var rowNumber = 0;
        foreach (var item in items)
        {
            rowNumber++;
            var displayedTaxRate = invoice.Snapshot?.TaxMode switch { "Global" => invoice.Snapshot.TaxRate, "No Tax" => 0m, _ => item.TaxRate };
            var presentation = invoice.Snapshot?.LinePresentations?.ElementAtOrDefault(rowNumber - 1);
            var itemText = options.ShowAliasName && !string.IsNullOrWhiteSpace(presentation?.Alias) ? presentation.Alias : item.Description;
            if (options.ShowProductServiceTag && !string.IsNullOrWhiteSpace(presentation?.ProductType)) itemText += " [" + presentation.ProductType + "]";
            var productDescription = !thermal && options.ShowDescription && !string.IsNullOrWhiteSpace(item.ProductDescription) && !item.ProductDescription.Equals(itemText, StringComparison.OrdinalIgnoreCase) ? item.ProductDescription : "";
            var firstLine = options.DescriptionOnNewLine || string.IsNullOrWhiteSpace(productDescription) ? itemText : itemText + " — " + productDescription;
            if (!thermal)
            {
                var itemLabel = firstLine + (options.DescriptionOnNewLine && productDescription.Length > 0 ? "\n" + productDescription : "");
                string Cell(string key) => key switch
                {
                    "serial" => rowNumber.ToString(CultureInfo.InvariantCulture),
                    "item" => itemLabel,
                    "hsn" => invoice.Snapshot?.LineHsnCodes?.ElementAtOrDefault(rowNumber - 1) ?? "",
                    "qty" => item.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
                    "price" => Money(item.UnitPrice),
                    "tax" => options.ShowGst && invoice.Snapshot?.IsInterState != true ? $"{displayedTaxRate / 2:0.##} / {displayedTaxRate / 2:0.##}" : displayedTaxRate.ToString("0.##", CultureInfo.InvariantCulture),
                    "discount" => Money(item.DiscountPerUnit ? item.Discount * item.Quantity : item.Discount),
                    "total" => Money(item.LineTotal),
                    _ => presentation?.Metadata.GetValueOrDefault(key[5..]) ?? ""
                };
                var cells = columns.Select((column, index) => Wrap(Cell(column.Key), widths[index] - 8, tableSize).ToArray()).ToArray();
                var lines = Math.Max(1, cells.Max(cell => cell.Length));
                for (var line = 0; line < lines; line++)
                {
                    if (y + tableSize + 8 > height - 45) { NewPage(); TableHeader(); }
                    var x = margin;
                    for (var col = 0; col < columns.Count; col++)
                    {
                        if (grid)
                        {
                            paint.Color = ruleColor; paint.Style = SKPaintStyle.Stroke;
                            canvas.DrawRect(x, y, widths[col], tableSize + 7, paint); paint.Style = SKPaintStyle.Fill;
                        }
                        if (line < cells[col].Length) Text(cells[col][line], x + 4, y + tableSize + 2, tableSize);
                        x += widths[col];
                    }
                    y += tableSize + 7;
                }
                Rule(y); y += grid ? 0 : 7;
                continue;
            }
            var description = Wrap(firstLine, narrow ? usable - 12 : usable * .49f, size).DefaultIfEmpty("Item").ToArray();
            if (y + size + 26 > height - 45) { NewPage(); TableHeader(); }
            Text(description[0], margin + 6, y, size, true);
            if (!narrow)
            {
                if (options.ShowSlNo) Text(rowNumber.ToString(CultureInfo.InvariantCulture), margin + usable * .52f, y, size, right: true);
                if (options.ShowQuantity) Text(item.Quantity.ToString("0.###", CultureInfo.InvariantCulture), margin + usable * .63f, y, size, right: true);
                if (options.ShowPrice) Text(Money(item.UnitPrice), margin + usable * .78f, y, size, right: true);
                if (options.ShowTax) Text(displayedTaxRate.ToString("0.##", CultureInfo.InvariantCulture), width - margin - 6, y, size, right: true);
            }
            y += size + 5;
            var hsn = invoice.Snapshot?.LineHsnCodes?.ElementAtOrDefault(rowNumber - 1);
            if (options.ShowGst && options.ShowHsn && !string.IsNullOrWhiteSpace(hsn)) Paragraph("HSN/SAC: " + hsn);
            foreach (var line in description.Skip(1).Concat(options.DescriptionOnNewLine && productDescription.Length > 0 ? Wrap(productDescription, narrow ? usable - 12 : usable * .49f, size) : []))
            {
                if (y + size + 6 > height - 45) { NewPage(); TableHeader(); }
                Text(line, margin + 6, y, size, color: muted); y += size + 5;
            }
            if (narrow)
            {
                var parts = new List<string>();
                if (options.ShowQuantity) parts.Add($"{quantityLabel} {item.Quantity:0.###}");
                parts.Add("Rate " + Money(item.UnitPrice));
                if (options.ShowTax) parts.Add($"Tax {displayedTaxRate:0.##}%");
                Paragraph(string.Join(" · ", parts));
            }
            if (options.ShowDiscount && item.Discount != 0) Paragraph("Item discount: " + Money(item.DiscountPerUnit ? item.Discount * item.Quantity : item.Discount));
            if (item.ExtraCost != 0) Paragraph("Item extra cost: " + Money(item.ExtraCost));
            Paragraph("Total " + Money(item.LineTotal), true);
            Ensure(10); Rule(y); y += compact || narrow ? 10 : 16;
        }
        Ensure(135); y += 6;
        if (!minimal && !narrow)
        {
            paint.Color = WithAlpha(accent, modern || executive ? (byte)20 : (byte)12);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(width - margin - Math.Min(usable, 245), y - 6, width - margin, y + 119), 6, 6), paint);
        }
        var currency = invoice.Snapshot?.Currency?.Split('—')[0].Trim() ?? "";
        Paragraph("AMOUNTS" + (currency.Length > 0 ? " · " + currency : ""), true);
        void Total(string label, decimal amount, bool strong = false) => TotalText(label, Money(amount), strong);
        void TotalText(string label, string value, bool strong = false)
        {
            Ensure(24); Text(label, margin, y, size, strong); Text(value, width - margin, y, size, strong, right: true); y += 21;
        }
        if (options.ShowTotalQuantity) TotalText("Total quantity", items.Sum(i => i.Quantity).ToString("0.###", CultureInfo.InvariantCulture));
        Total("Subtotal", invoice.SubTotal);
        if (options.ShowTax) Total("Tax", invoice.TaxTotal);
        if (options.ShowDiscount && invoice.DiscountTotal != 0) Total("Discount", invoice.DiscountTotal);
        foreach (var cost in invoice.Snapshot?.AdditionalCosts ?? [])
            if (cost.Amount != 0) Total(string.IsNullOrWhiteSpace(cost.Description) ? "Charges and adjustments" : cost.Description, cost.Amount);
        Total("Total", invoice.GrandTotal, true);
        if (options.PreviousBalance > 0) Total("Previous balance due", options.PreviousBalance);
        if (options.ShowRoundOff)
        {
            var amount = invoice.GrandTotal + options.PreviousBalance;
            var rounded = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            Total("Round off", rounded - amount);
            Total("Net Amount", rounded, true);
            Paragraph(LedgerNest.Application.AmountInWords.Format(rounded, invoice.Snapshot?.Currency.Contains("INR", StringComparison.Ordinal) != false));
        }
        Total("Paid", invoice.PaidAmount);
        Total("Balance due", invoice.GrandTotal - invoice.PaidAmount, true);
        if (options.PreviousBalance > 0) Total("Total due", Math.Max(0, invoice.GrandTotal - invoice.PaidAmount) + options.PreviousBalance, true);
        y += 8; Paragraph(invoice.Snapshot?.Notes); Paragraph(options.AdditionalInformation); Paragraph(business.Note);
        using var signature = DecodeImage(options.SignatureImage);
        if (signature != null)
        {
            var scale = Math.Min(usable / signature.Width, options.SignatureSize / signature.Height);
            var signatureHeight = signature.Height * scale;
            Ensure(signatureHeight + 36);
            var x = options.SignaturePosition == "Left" ? margin : width - margin - signature.Width * scale;
            canvas.DrawBitmap(signature, new SKRect(x, y, x + signature.Width * scale, y + signatureHeight));
            y += signatureHeight + 15;
            Text("Authorised Signature", options.SignaturePosition == "Left" ? margin : width - margin, y, size, right: options.SignaturePosition != "Left");
        }
        FinishPage(); pdf.Close();
        return output.ToArray();
    }
}
