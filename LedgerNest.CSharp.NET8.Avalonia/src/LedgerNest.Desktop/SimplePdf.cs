using System.Text;

namespace LedgerNest.Desktop;

internal static class SimplePdf
{
    // Performs the create action for this screen or workflow.
    public static byte[] Create(IEnumerable<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 14 Tf");
        content.AppendLine("50 790 Td");
        foreach (var line in lines)
        {
            content.Append("(").Append(Escape(line)).AppendLine(") Tj");
            content.AppendLine("0 -20 Td");
        }
        content.AppendLine("ET");

        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"
        };

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        var offsets = new List<int> { 0 };
        foreach (var (obj, index) in objects.Select((value, i) => (value, i + 1)))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index).AppendLine(" 0 obj");
            pdf.AppendLine(obj);
            pdf.AppendLine("endobj");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.Append("0 ").AppendLine((objects.Length + 1).ToString());
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) pdf.Append(offset.ToString("0000000000")).AppendLine(" 00000 n ");
        pdf.AppendLine("trailer");
        pdf.Append("<< /Size ").Append(objects.Length + 1).AppendLine(" /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xref.ToString());
        pdf.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    // Performs the escape action for this screen or workflow.
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
