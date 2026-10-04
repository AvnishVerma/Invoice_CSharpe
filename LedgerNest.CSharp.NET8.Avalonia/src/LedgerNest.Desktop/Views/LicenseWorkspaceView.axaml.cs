using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts a license-state notice above the current workspace content.</summary>
public sealed partial class LicenseWorkspaceView : UserControl
{
    private readonly Action? manageLicense;
    public static readonly StyledProperty<object?> BodyProperty = AvaloniaProperty.Register<LicenseWorkspaceView, object?>(nameof(Body));
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public LicenseWorkspaceView()
    {
        InitializeComponent();
    }

    public LicenseWorkspaceView(MainWindowViewModel model, Control body, Action manageLicense)
        : this()
    {
        DataContext = model;
        Body = body;
        this.manageLicense = manageLicense;
    }
    private void ManageLicense(object? sender, RoutedEventArgs args) => manageLicense?.Invoke();
}
