using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

// Native drawing port of TemplatePreviewSketch in widgets/template_list_tile.dart.
internal sealed class TemplateSketch(string template, IBrush accent, bool details = true) : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var pad = details ? 24d : 4d;
        var w = Math.Max(1, Bounds.Width - pad * 2); var h = Math.Max(1, Bounds.Height - pad * 2);
        var grey = Brush.Parse("#E5E7EB"); var pale = Brush.Parse("#F8FAFC"); var border = Brush.Parse("#CBD5E1");
        double D(double full, double small) => details ? full : small;
        void Box(double x, double y, double width, double height, IBrush? color = null, double radius = 0) => context.DrawRectangle(color ?? grey, null, new Rect(pad + x, pad + y, Math.Max(0, width), Math.Max(0, height)), radius, radius);
        void Line(double x, double y, double width, double? height = null, IBrush? color = null) => Box(x, y, width, height ?? D(6, 3), color, 2);
        void Table(double y, bool filled = true)
        { Box(0, y, w, D(22, 5), filled ? accent : grey); for (var i = 0; i < (details ? 5 : 3); i++) Box(0, y + D(22, 5) + (i + 1) * D(2, 1) + i * D(26, 4), w, D(26, 4), i % 2 == 0 ? pale : Brushes.White); }
        void Totals(double bottom = 0)
        { Box(w - D(130, 34), h - bottom - D(58, 10), D(130, 34), D(58, 10), Brush.Parse("#F1F5F9"), 4); Box(w - D(130, 34), h - bottom - D(18, 3), D(130, 34), D(18, 3), accent); }
        context.DrawRectangle(Brushes.White, null, new Rect(Bounds.Size));
        using var clip = context.PushClip(new Rect(Bounds.Size));
        switch (template)
        {
            case "Modern":
                Box(0, 0, w, D(96, 22), accent); Line(D(16, 4), D(16, 4), (w - D(32, 8)) * .45, D(10, 3), Brushes.White); Line(D(16, 4), D(34, 10), (w - D(32, 8)) * .7, D(7, 2.5), Brush.Parse("#B3FFFFFF"));
                Table(D(120, 25)); Totals(D(52, 9)); Box(0, h - D(34, 7), w, D(34, 7), accent); break;
            case "Minimal":
                Line(0, 0, D(160, 36) * .7); Line(0, D(11, 6), D(160, 36) * .5); Box(w - D(48, 14), 0, D(48, 14), D(38, 12));
                Box(0, D(60, 17), w, 1, border); Table(D(89, 24), false); Totals(D(18, 4)); Box(0, h - D(2, 1), w, D(2, 1), accent); break;
            case "Executive":
                Box(0, 0, D(8, 3), D(72, 14), accent); Line(D(22, 7), 0, (w - D(112, 31)) * .55, D(11, 3)); Line(D(22, 7), D(17, 6), (w - D(112, 31)) * .8, D(7, 2.5)); Line(w - D(72, 16), 0, D(72, 16), D(18, 5), accent);
                Box(0, D(96, 16), (w - D(16, 4)) / 2, D(72, 12), pale); Box((w + D(16, 4)) / 2, D(96, 16), (w - D(16, 4)) / 2, D(72, 12), pale);
                Table(D(192, 30)); Totals(D(21, 2.5)); Box(0, h - D(3, 1.5), w, D(3, 1.5), accent); break;
            case "Compact":
                Box(0, 0, D(40, 10), D(32, 9)); Line(D(48, 12), 0, (w - D(108, 30)) * .8, D(9, 3)); Line(D(48, 12), D(13, 5), (w - D(108, 30)) * .6, D(6, 2)); Line(w - D(52, 14), 0, D(52, 14), D(11, 3), accent);
                context.DrawRectangle(null, new Pen(border, .5), new Rect(pad, pad + D(40, 11), w, D(36, 8))); Table(D(82, 21)); Box(0, D(248, 42), w, D(14, 3), Brush.Parse("#F1F5F9")); Totals(); break;
            case "Grid Classic":
                context.DrawRectangle(null, new Pen(Brush.Parse("#334155"), D(1.2, 1)), new Rect(pad, pad, w, h));
                Line((w - D(110, 26)) / 2, D(10, 3), D(110, 26), D(8, 3), accent); Line((w - D(80, 20)) / 2, D(23, 8), D(80, 20), D(5, 2)); Box(D(10, 3), D(38, 13), w - D(20, 6), 1, border);
                Line(D(10, 3), D(49, 17), w * .42, D(6, 2)); Line(D(10, 3), D(59, 20), w * .3, D(6, 2)); Line(w * .64, D(49, 17), w * .28, D(6, 2)); Line(w * .64, D(59, 20), w * .22, D(6, 2));
                var tableY = D(75, 25); var tableH = D(74, 20); var tableW = w - D(20, 6);
                context.DrawRectangle(null, new Pen(border, .6), new Rect(pad + D(10, 3), pad + tableY, tableW, tableH)); Box(D(10, 3), tableY, tableW, D(16, 4), Brush.Parse("#E2E8F0"));
                foreach (var factor in new[] { 1d / 6, 4d / 6, 5d / 6 }) Box(D(10, 3) + tableW * factor, tableY + D(16, 4), .6, tableH - D(16, 4), border);
                Line(w - D(98, 25), tableY + tableH + D(10, 3), D(88, 22), D(5, 2)); Line(w - D(98, 25), tableY + tableH + D(18, 6), D(88, 22), D(5, 2)); Line(w - D(80, 21), tableY + tableH + D(28, 9), D(70, 18), D(7, 3), accent); break;
            case "Thermal":
                var tw = w * .62; var tx = (w - tw) / 2;
                void Dash(double y) { var count = details ? 24 : 12; for (var i = 0; i < count; i += 2) Box(tx + i * tw / count + D(1, .5), y, tw / count - D(2, 1), 1, border); }
                var start = Math.Max(0, (h - D(370, 68)) / 2); Dash(start); Line((w - D(90, 22)) / 2, start + D(9, 3), D(90, 22), D(7, 3), accent); Line((w - D(70, 16)) / 2, start + D(20, 7), D(70, 16), D(4, 2)); Line((w - D(60, 14)) / 2, start + D(26, 10), D(60, 14), D(4, 2)); Dash(start + D(38, 14));
                var rowY = start + D(45, 17); for (var i = 0; i < (details ? 20 : 5); i++) { Line(tx, rowY, (tw - D(26, 8)) * .6, D(5, 2)); Line(tx + tw - D(20, 6), rowY, D(20, 6), D(5, 2)); rowY += D(13, 4); }
                Dash(rowY); Line(tx, rowY + D(7, 3), D(40, 10), D(7, 3), accent); Line(tx + tw - D(40, 10), rowY + D(7, 3), D(40, 10), D(7, 3), accent); Dash(rowY + D(22, 8)); break;
            default:
                Box(0, 0, D(54, 14), D(42, 12)); Line(w - D(170, 38), D(10.5, 1), D(170, 38), D(9, 3)); Line(w - D(170, 38), D(23.5, 8), D(170, 38) * .72, D(7, 3));
                Box(0, D(58, 16), w, D(3, 1.5), accent); Line(0, D(83, 22.5), w * .28); Table(D(107, 29.5)); Totals(); break;
        }
    }
}
