using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class BackupManagementView : UserControl
{
    public BackupManagementView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as BackupManagementViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as BackupManagementViewModel)?.Detach();
    }
}
