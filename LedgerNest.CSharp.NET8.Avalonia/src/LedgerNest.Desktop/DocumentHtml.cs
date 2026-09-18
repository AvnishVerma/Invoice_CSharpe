using System.Globalization;
using System.Net;
using System.Text;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

// Creates self-contained invoice HTML for Chromium rendering and direct printing.
internal static class DocumentHtml
{
    // Builds printable HTML using the same invoice data and configured layout options as PDF export.
    public static string Create(
        Invoice invoice,
        InvoiceItem[] items,
        DocumentPdf.Business business,
        string pageSize,
        bool landscape,
        string template,
        string themeColor,
        DocumentPdf.PdfExportOptions options)
    {
        var accent = IsHexColor(themeColor) ? themeColor : "#0F766E";
        var templateClass = CssClass(string.IsNullOrWhiteSpace(template) ? "Classic" : template);
        var thermal = template.Equals("Thermal", StringComparison.OrdinalIgnoreCase) || pageSize.StartsWith("Thermal", StringComparison.OrdinalIgnoreCase);
        var pageRule = PageRule(pageSize, landscape, thermal);
        var customer = invoice.Snapshot?.Customer;
        var title = string.IsNullOrWhiteSpace(invoice.Snapshot?.DocumentTitle) ? invoice.Type : invoice.Snapshot.DocumentTitle;
        var html = new StringBuilder();
        html.Append("""
<!doctype html>
<html><head><meta charset="utf-8"><style>
*{box-sizing:border-box}html,body{margin:0;padding:0;background:#fff;color:#172B3A;font-family:Arial,sans-serif;font-size:12px}
@page{margin:0;SIZE_RULE}body{print-color-adjust:exact;-webkit-print-color-adjust:exact}
.sheet{width:100%;min-height:100vh;padding:32px;position:relative}.top-rule{height:6px;background:ACCENT;margin:-32px -32px 24px}
.header{display:flex;justify-content:space-between;gap:24px;align-items:flex-start}.title{font-size:28px;font-weight:700;color:ACCENT;text-transform:uppercase}.number{margin-top:5px;color:#52616B}
.business{margin-top:18px;line-height:1.55}.business-name{font-size:16px;font-weight:700}.logo{max-width:180px;max-height:56px;margin-bottom:10px}
.rule{border-top:1px solid #DDE5E7;margin:18px 0}.meta{display:grid;grid-template-columns:1fr 1fr;gap:24px;line-height:1.6}.customer-name{font-weight:700}
table{width:100%;border-collapse:collapse;margin-top:20px;page-break-inside:auto}thead{display:table-header-group}tr{page-break-inside:avoid}th{background:ACCENT;color:white;text-align:left;padding:9px 8px;font-size:11px}td{padding:10px 8px;border-bottom:1px solid #DDE5E7;vertical-align:top}.number-cell{text-align:right;white-space:nowrap}.description{color:#52616B;font-size:11px;margin-top:4px}
.summary{width:min(340px,100%);margin:22px 0 0 auto;background:#F7F2FA;border-radius:8px;padding:14px}.summary-row{display:flex;justify-content:space-between;padding:4px 0}.summary-row.total{font-size:15px;font-weight:700;border-top:1px solid #DDE5E7;margin-top:5px;padding-top:10px}
.notes{white-space:pre-wrap;margin-top:22px;line-height:1.5}.footer{margin-top:24px;border-top:1px solid #DDE5E7;padding-top:10px;color:#52616B;font-size:10px}
.modern .top-rule{height:82px;margin-bottom:-58px}.modern .title,.modern .number{color:white;position:relative}.executive{border-left:14px solid ACCENT}.executive .top-rule{display:none}.executive .header{background:#F2F7F7;padding:16px;border-radius:8px}.minimal .top-rule{height:1px;margin-top:12px}.minimal th{background:white;color:#172B3A;border-bottom:2px solid ACCENT}.grid-classic th,.grid-classic td{border:1px solid #DDE5E7}.grid-classic th{background:#F7F2FA;color:#172B3A}.compact{padding:24px}.compact .top-rule{margin:-24px -24px 18px;height:4px}.compact td{padding:7px}.thermal{padding:12px;font-size:9px}.thermal .top-rule{margin:-12px -12px 10px;height:4px}.thermal .header{display:block;text-align:center}.thermal .title{font-size:16px}.thermal .meta{display:block}.thermal th,.thermal td{padding:5px 3px;font-size:8px}.thermal .optional-wide{display:none}.thermal .summary{margin-top:12px}
</style></head><body><main class="sheet TEMPLATE_CLASS">
""".Replace("SIZE_RULE", pageRule).Replace("ACCENT", accent).Replace("TEMPLATE_CLASS", thermal ? "thermal" : templateClass));

        html.Append("<div class=\"top-rule\"></div><header class=\"header\"><div><div class=\"title\">")
            .Append(E(title)).Append("</div>");
        if (invoice.Snapshot?.HideInvoiceNumber != true)
            html.Append("<div class=\"number\">#").Append(E(invoice.InvoiceNumber)).Append("</div>");
        html.Append("</div><div>").Append(LogoHtml(business.Logo)).Append("</div></header>");
        html.Append("<section class=\"business\"><div class=\"business-name\">").Append(E(string.IsNullOrWhiteSpace(business.Name) ? Branding.Name : business.Name)).Append("</div>")
            .Append(Line(business.Address)).Append(Line(business.Phone)).Append(Line(business.Email));
        if (!string.IsNullOrWhiteSpace(business.TaxId)) html.Append("<div>GST / Tax ID: ").Append(E(business.TaxId)).Append("</div>");
        html.Append("</section><div class=\"rule\"></div><section class=\"meta\"><div><div>Date: ").Append(E(DateText(invoice.InvoiceDate, options))).Append("</div><div>Status: ").Append(E(invoice.Status)).Append("</div></div><div><div class=\"customer-name\">Customer: ").Append(E(customer?.Name ?? invoice.CustomerName)).Append("</div>");
        if (options.ShowCustomerBusinessName) html.Append(Line(customer?.BusinessName, "Business: "));
        if (options.ShowCustomerAddress) html.Append(Line(customer?.Address));
        if (options.ShowCustomerPhone) html.Append(Line(customer?.Phone));
        if (options.ShowCustomerEmail) html.Append(Line(customer?.Email));
        if (options.ShowCustomerGstin) html.Append(Line(customer?.GstNumber, "GSTIN: "));
        html.Append("</div></section><table><thead><tr>");
        if (options.ShowSlNo) html.Append("<th>#</th>");
        html.Append("<th>").Append(options.ShowItemName ? "ITEM / SERVICE" : "DETAILS").Append("</th>");
        if (options.ShowQuantity) html.Append("<th class=\"number-cell\">").Append(E(string.IsNullOrWhiteSpace(options.QuantityLabel) ? "Qty" : options.QuantityLabel)).Append("</th>");
        if (options.ShowPrice) html.Append("<th class=\"number-cell optional-wide\">RATE</th>");
        if (options.ShowTax) html.Append("<th class=\"number-cell optional-wide\">TAX %</th>");
        if (options.ShowDiscount) html.Append("<th class=\"number-cell optional-wide\">DISCOUNT</th>");
        html.Append("<th class=\"number-cell\">TOTAL</th></tr></thead><tbody>");
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            html.Append("<tr>");
            if (options.ShowSlNo) html.Append("<td>").Append(index + 1).Append("</td>");
            html.Append("<td><strong>").Append(E(options.ShowItemName ? item.Description : "Item")).Append("</strong>");
            if (options.ShowDescription && !string.IsNullOrWhiteSpace(item.ProductDescription) && !item.ProductDescription.Equals(item.Description, StringComparison.OrdinalIgnoreCase))
                html.Append("<div class=\"description\">").Append(E(item.ProductDescription)).Append("</div>");
            html.Append("</td>");
            if (options.ShowQuantity) html.Append(Cell(item.Quantity.ToString("0.###", CultureInfo.InvariantCulture)));
            if (options.ShowPrice) html.Append(Cell(Money(item.UnitPrice), true));
            if (options.ShowTax) html.Append(Cell(item.TaxRate.ToString("0.##", CultureInfo.InvariantCulture), true));
            if (options.ShowDiscount) html.Append(Cell(Money(item.DiscountPerUnit ? item.Discount * item.Quantity : item.Discount), true));
            html.Append(Cell(Money(item.LineTotal), false)).Append("</tr>");
        }
        html.Append("</tbody></table><section class=\"summary\">");
        if (options.ShowTotalQuantity) html.Append(Summary("Total quantity", items.Sum(item => item.Quantity).ToString("0.###", CultureInfo.InvariantCulture)));
        html.Append(Summary("Subtotal", Money(invoice.SubTotal)));
        if (options.ShowTax) html.Append(Summary("Tax", Money(invoice.TaxTotal)));
        if (options.ShowDiscount && invoice.DiscountTotal != 0) html.Append(Summary("Discount", Money(invoice.DiscountTotal)));
        foreach (var cost in invoice.Snapshot?.AdditionalCosts ?? [])
            if (cost.Amount != 0) html.Append(Summary(string.IsNullOrWhiteSpace(cost.Description) ? "Charges and adjustments" : cost.Description, Money(cost.Amount)));
        if (options.ShowTotal) html.Append(Summary("Total", Money(invoice.GrandTotal), true));
        html.Append(Summary("Paid", Money(invoice.PaidAmount))).Append(Summary("Balance due", Money(Math.Max(0, invoice.GrandTotal - invoice.PaidAmount)), true)).Append("</section>");
        if (!string.IsNullOrWhiteSpace(invoice.Snapshot?.Notes) || !string.IsNullOrWhiteSpace(business.Note))
            html.Append("<section class=\"notes\">").Append(E(invoice.Snapshot?.Notes)).Append(Line(business.Note)).Append("</section>");
        html.Append("<footer class=\"footer\">").Append(E($"{Branding.Name} · {invoice.Type} · {template}")).Append("</footer></main></body></html>");
        return html.ToString();
    }

    // Maps the selected paper size and orientation into a CSS print-page rule.
    private static string PageRule(string pageSize, bool landscape, bool thermal) => thermal
        ? pageSize.Contains("58", StringComparison.Ordinal) ? "size:58mm auto;" : "size:80mm auto;"
        : $"size:{(pageSize is "A5" or "A6" ? pageSize : "A4")} {(landscape ? "landscape" : "portrait")};";

    // Formats a configured invoice date without allowing an invalid format to stop printing.
    private static string DateText(DateTime value, DocumentPdf.PdfExportOptions options)
    {
        try
        {
            var date = value.ToString(string.IsNullOrWhiteSpace(options.DateFormat) ? "dd/MM/yyyy" : options.DateFormat, CultureInfo.InvariantCulture);
            if (!options.ShowTime) return date;
            return date + " " + value.ToString(options.TimeFormat.Contains("12", StringComparison.OrdinalIgnoreCase) ? "hh:mm tt" : "HH:mm", CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
    }

    // Converts a configured logo path or base64 payload into an embedded image tag.
    private static string LogoHtml(string logo)
    {
        try
        {
            if (logo.StartsWith("base64:", StringComparison.Ordinal)) return $"<img class=\"logo\" src=\"data:image/png;base64,{E(logo[7..])}\">";
            if (File.Exists(logo)) return $"<img class=\"logo\" src=\"data:image/{ImageExtension(logo)};base64,{Convert.ToBase64String(File.ReadAllBytes(logo))}\">";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException) { }
        return "";
    }

    // Returns a browser-compatible image subtype for embedded company logos.
    private static string ImageExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "jpeg", ".svg" => "svg+xml", _ => "png" };

    // Creates one numeric invoice table cell.
    private static string Cell(string value, bool optional = false) => $"<td class=\"number-cell{(optional ? " optional-wide" : "")}\">{E(value)}</td>";

    // Creates one label/value row in the invoice total summary.
    private static string Summary(string label, string value, bool total = false) => $"<div class=\"summary-row{(total ? " total" : "")}\"><span>{E(label)}</span><span>{E(value)}</span></div>";

    // Creates an encoded non-empty text line with an optional prefix.
    private static string Line(string? value, string prefix = "") => string.IsNullOrWhiteSpace(value) ? "" : $"<div>{E(prefix + value)}</div>";

    // Formats an invoice amount for HTML output.
    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    // Encodes application data before inserting it into HTML.
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    // Converts a display template name into a safe CSS class.
    private static string CssClass(string value) => string.Join('-', value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray())));

    // Accepts only six-digit CSS hex colors from configuration.
    private static bool IsHexColor(string value) => value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit);
}
