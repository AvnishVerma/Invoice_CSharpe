using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class FormToggleSwitchView : UserControl
{
    public static readonly StyledProperty<FormField?> FieldProperty = AvaloniaProperty.Register<FormToggleSwitchView, FormField?>(nameof(Field));
    public FormField? Field { get => GetValue(FieldProperty); set => SetValue(FieldProperty, value); }
    public FormToggleSwitchView() => InitializeComponent();
}
