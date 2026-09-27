using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop;

namespace LedgerNest.Desktop.Views;

/// <summary>Renders the standard invoice-setting toggle with XAML-owned layout and theme resources.</summary>
public sealed partial class InvoiceToggleSettingView : UserControl
{
    public InvoiceToggleSettingView()
    {
        InitializeComponent();
    }

    public InvoiceToggleSettingView(FormField field, string title, string help, string icon)
        : this()
    {
        TitleText.Text = title;
        HelpText.Text = help;
        HelpText.IsVisible = !string.IsNullOrWhiteSpace(help);
        IconText.Text = icon;
        IconText.IsVisible = !string.IsNullOrWhiteSpace(icon);
        Toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FormField.IsChecked)) { Source = field, Mode = BindingMode.TwoWay });
        AutomationProperties.SetName(Toggle, field.Label);
        Toggle.IsCheckedChanged += (_, _) => Paint();
        Paint();
    }

    private void Paint()
    {
        var selected = Toggle.IsChecked == true;
        Track.Background = selected ? Brush.Parse("#8097BD") : Brushes.White;
        Track.BorderBrush = selected ? Brushes.Transparent : Brush.Parse("#BDBDBD");
        Thumb.Fill = selected ? Brush.Parse("#0D47A1") : Brush.Parse("#BDBDBD");
        Thumb.HorizontalAlignment = selected ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }
}
