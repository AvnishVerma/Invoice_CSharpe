using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the XAML settings panel frame used by every invoice-settings section.</summary>
public sealed partial class InvoiceSettingsPanelView : UserControl
{
    public InvoiceSettingsPanelView()
    {
        InitializeComponent();
    }

    public InvoiceSettingsPanelView(string heading, Control fields)
        : this()
    {
        HeadingText.Text = heading;
        FieldsHost.Content = fields;
    }
}
