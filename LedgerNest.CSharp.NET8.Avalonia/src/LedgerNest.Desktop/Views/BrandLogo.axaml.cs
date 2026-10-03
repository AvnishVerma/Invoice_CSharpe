using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class BrandLogo : UserControl
{
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<BrandLogo, bool>(nameof(Compact));
    public bool Compact { get => GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    public BrandLogo() => InitializeComponent();
    public BrandLogo(bool compact) : this() => Compact = compact;
}
