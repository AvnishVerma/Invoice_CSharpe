using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class DashboardPageView : UserControl
{
    public DashboardPageView()
    {
        InitializeComponent();
    }

    public DashboardPageView(Control appBar, Control body)
    {
        InitializeComponent();
        AppBarHost.Content = appBar;
        BodyHost.Content = body;
    }
}
