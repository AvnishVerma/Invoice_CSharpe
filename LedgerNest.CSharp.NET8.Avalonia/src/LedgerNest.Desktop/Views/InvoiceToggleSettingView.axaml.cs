using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceToggleSettingView : UserControl
{
    public static readonly StyledProperty<FormField?> FieldProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, FormField?>(nameof(Field));
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, string>(nameof(Title), "");
    public static readonly StyledProperty<string> HelpProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, string>(nameof(Help), "");
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, string>(nameof(Icon), "");
    public FormField? Field { get => GetValue(FieldProperty); set => SetValue(FieldProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Help { get => GetValue(HelpProperty); set => SetValue(HelpProperty, value); }
    public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public InvoiceToggleSettingView() => InitializeComponent();
    public InvoiceToggleSettingView(FormField field, string title, string help, string icon) : this()
    {
        Field = field;
        Title = title;
        Help = help;
        Icon = icon;
    }
}
