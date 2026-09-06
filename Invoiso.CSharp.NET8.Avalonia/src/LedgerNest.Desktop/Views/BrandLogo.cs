using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

/// <summary>Resolution-independent receipt mark and wordmark shared by all brand surfaces.</summary>
public sealed class BrandLogo : StackPanel
{
    public BrandLogo(bool compact = false)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        VerticalAlignment = VerticalAlignment.Center;
        var strokes = Ui.Stack(4);
        foreach (var width in new[] { 16, 16, 10 })
            strokes.Children.Add(new Border { Width = width, Height = 2, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left });
        Children.Add(new Border { Width = 32, Height = 36, CornerRadius = new CornerRadius(5, 5, 10, 5), Background = Ui.Primary, Padding = new Thickness(8, 10), Child = strokes });
        if (!compact) Children.Add(Ui.Text(Branding.Name, 18, true, Brush.Parse(Branding.InkColor)));
        Avalonia.Automation.AutomationProperties.SetName(this, Branding.Name);
    }
}
