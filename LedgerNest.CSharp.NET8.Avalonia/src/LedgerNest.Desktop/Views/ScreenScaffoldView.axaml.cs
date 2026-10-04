using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the shared XAML page frame used by settings and management content.</summary>
public sealed partial class ScreenScaffoldView : UserControl
{
    public static readonly StyledProperty<object?> PageHeaderProperty = AvaloniaProperty.Register<ScreenScaffoldView, object?>(nameof(PageHeader));
    public static readonly StyledProperty<object?> PageBodyProperty = AvaloniaProperty.Register<ScreenScaffoldView, object?>(nameof(PageBody));
    public static readonly StyledProperty<bool> ShowHeaderProperty = AvaloniaProperty.Register<ScreenScaffoldView, bool>(nameof(ShowHeader), true);
    public object? VisibleHeader => ShowHeader ? PageHeader : null;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowHeaderProperty || change.Property == PageHeaderProperty)
            RaisePropertyChanged(VisibleHeaderProperty, null, VisibleHeader);
    }
    public static readonly DirectProperty<ScreenScaffoldView, object?> VisibleHeaderProperty = AvaloniaProperty.RegisterDirect<ScreenScaffoldView, object?>(nameof(VisibleHeader), view => view.VisibleHeader);
    public object? PageHeader { get => GetValue(PageHeaderProperty); set => SetValue(PageHeaderProperty, value); }
    public object? PageBody { get => GetValue(PageBodyProperty); set => SetValue(PageBodyProperty, value); }
    public bool ShowHeader { get => GetValue(ShowHeaderProperty); set => SetValue(ShowHeaderProperty, value); }
    public ScreenScaffoldView()
    {
        InitializeComponent();
    }

    public ScreenScaffoldView(Control header, Control body)
        : this()
    {
        PageHeader = header;
        PageBody = body;
    }
}
