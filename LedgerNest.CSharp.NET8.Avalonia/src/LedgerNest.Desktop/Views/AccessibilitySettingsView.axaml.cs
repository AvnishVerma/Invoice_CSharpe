using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class AccessibilitySettingsView : UserControl
{
    public AccessibilitySettingsView() => InitializeComponent();
    public AccessibilitySettingsView(Action save) : this()
        => DataContext = new SettingsFormPageViewModel("Accessibility", [], save);
}
