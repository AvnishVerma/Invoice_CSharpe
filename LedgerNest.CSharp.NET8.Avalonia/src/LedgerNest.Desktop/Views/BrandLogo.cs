using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

/// <summary>Invoiso logo surface shared by the sidebar and compact shell states.</summary>
public sealed class BrandLogo : Image
{
    public BrandLogo() : this(false)
    {
    }

    public BrandLogo(bool compact)
    {
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        Width = compact ? 42 : 112;
        Height = compact ? 42 : 58;
        Stretch = Stretch.Uniform;
        Source = compact ? Ui.AssetBitmap("logo_v.png") : Ui.AssetBitmap("logo.png");
        Avalonia.Automation.AutomationProperties.SetName(this, Branding.Name);
    }
}
