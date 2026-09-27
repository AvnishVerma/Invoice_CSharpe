using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class SettingsSaveRailView : UserControl
{
    public SettingsSaveRailView() => InitializeComponent();

    public SettingsSaveRailView(Control save) : this() => SaveHost.Content = save;
}
