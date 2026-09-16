using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

/// <summary>Brand mark and wordmark shared by sidebar surfaces.</summary>
public sealed class BrandLogo : StackPanel
{
    public BrandLogo() : this(false)
    {
    }

    public BrandLogo(bool compact)
    {
        Orientation = Orientation.Horizontal;
        Spacing = compact ? 0 : 8;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        var mark = new Border
        {
            Width = compact ? 36 : 34,
            Height = compact ? 36 : 34,
            CornerRadius = new CornerRadius(6),
            Background = Ui.Primary,
            Child = Ui.Icon("receipt_long", compact ? 22 : 21, Brushes.White)
        };
        Children.Add(mark);
        if (!compact)
        {
            Children.Add(Ui.Text(Branding.Name, 18, true, Ui.Palette(Branding.InkColor, "#E7F7F3")));
        }
        Avalonia.Automation.AutomationProperties.SetName(this, Branding.Name);
    }
}
