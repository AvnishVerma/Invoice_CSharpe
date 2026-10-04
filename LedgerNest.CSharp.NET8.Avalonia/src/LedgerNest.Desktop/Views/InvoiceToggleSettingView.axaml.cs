using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceToggleSettingView : UserControl
{
    public static readonly StyledProperty<FormField?> FieldProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, FormField?>(nameof(Field));
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, string>(nameof(Title), "");
    public static readonly StyledProperty<string> HelpProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, string>(nameof(Help), "");
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, string>(nameof(Icon), "");
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, bool>(nameof(Compact));
    public static readonly StyledProperty<bool> LeadingProperty = AvaloniaProperty.Register<InvoiceToggleSettingView, bool>(nameof(Leading));
    public static readonly DirectProperty<InvoiceToggleSettingView, InvoiceToggleLayoutModel> LayoutDataProperty = AvaloniaProperty.RegisterDirect<InvoiceToggleSettingView, InvoiceToggleLayoutModel>(nameof(LayoutData), view => view.LayoutData);
    public FormField? Field { get => GetValue(FieldProperty); set => SetValue(FieldProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Help { get => GetValue(HelpProperty); set => SetValue(HelpProperty, value); }
    public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool Compact { get => GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    public bool Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    public InvoiceToggleLayoutModel LayoutData => new(Field, Title, Help, Icon);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FieldProperty || change.Property == TitleProperty || change.Property == HelpProperty || change.Property == IconProperty)
            RaisePropertyChanged(LayoutDataProperty, null!, LayoutData);
    }

    public InvoiceToggleSettingView() => InitializeComponent();
    public InvoiceToggleSettingView(FormField field, string title, string help, string icon) : this()
    {
        Field = field;
        Title = title;
        Help = help;
        Icon = icon;
    }
}

public sealed record InvoiceToggleLayoutModel(FormField? Field, string Title, string Help, string Icon);
