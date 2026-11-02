using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class CommunicationSettingsView : UserControl
{
    public CommunicationSettingsView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => (DataContext as CommunicationSettingsViewModel)?.Dispose();
    }
}
