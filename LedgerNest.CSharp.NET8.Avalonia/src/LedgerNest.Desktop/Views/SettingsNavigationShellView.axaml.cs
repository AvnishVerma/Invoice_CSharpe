using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the responsive XAML navigation rail used by settings forms with section navigation.</summary>
public sealed partial class SettingsNavigationShellView : UserControl
{
    public SettingsNavigationShellView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
    }

    public SettingsNavigationShellView(Control navigation, Control promo, Control save, Control content)
        : this()
    {
        NavigationHost.Content = navigation;
        PromoHost.Content = promo;
        SaveHost.Content = save;
        ContentHost.Content = content;
        AttachedToVisualTree += (_, _) => ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var narrow = Bounds.Width > 0 && Bounds.Width < 760;
        ShellGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "240,*");
        ShellGrid.RowDefinitions = new RowDefinitions(narrow ? "190,*" : "*");
        Grid.SetColumn(Rail, 0);
        Grid.SetRow(Rail, 0);
        Grid.SetColumn(ContentHost, narrow ? 0 : 1);
        Grid.SetRow(ContentHost, narrow ? 1 : 0);
        PromoBorder.IsVisible = !narrow;
    }
}
