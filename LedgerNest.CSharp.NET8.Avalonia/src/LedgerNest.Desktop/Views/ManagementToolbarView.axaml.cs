using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementToolbarView : UserControl
{
    public ManagementToolbarView() => InitializeComponent();

    public ManagementToolbarView(Control search, Control actions, Control tabs, bool showTabs) : this()
    {
        SearchHost.Content = search;
        ActionsHost.Content = actions;
        TabsHost.Content = tabs;
        TabsHost.IsVisible = showTabs;
    }
}
