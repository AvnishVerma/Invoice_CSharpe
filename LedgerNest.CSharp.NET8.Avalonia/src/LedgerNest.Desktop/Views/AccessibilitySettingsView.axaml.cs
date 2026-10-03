using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class AccessibilitySettingsView : UserControl
{
    private readonly Action save;

    public AccessibilitySettingsView()
    {
        save = () => { };
        InitializeComponent();
    }

    public AccessibilitySettingsView(Action save) : this() => this.save = save;

    private void SaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => save();
}
