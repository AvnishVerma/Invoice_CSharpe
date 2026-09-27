using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the XAML layout for backup actions and the generated backup history list.</summary>
public sealed partial class BackupManagementView : UserControl
{
    public BackupManagementView()
    {
        InitializeComponent();
    }

    public BackupManagementView(Control actions, Control history)
        : this()
    {
        ActionsHost.Content = actions;
        HistoryHost.Content = history;
    }
}
