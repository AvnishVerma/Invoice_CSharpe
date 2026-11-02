using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the responsive XAML navigation rail used by settings forms with section navigation.</summary>
public sealed partial class SettingsNavigationShellView : UserControl
{
    public Control? Navigation { get => NavigationHost.Content as Control; set => NavigationHost.Content = value; }
    public Control? Promo { get => PromoHost.Content as Control; set => PromoHost.Content = value; }
    public Control? SaveAction { get => SaveHost.Content as Control; set => SaveHost.Content = value; }
    public Control? Body { get => ContentHost.Content as Control; set => ContentHost.Content = value; }
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
