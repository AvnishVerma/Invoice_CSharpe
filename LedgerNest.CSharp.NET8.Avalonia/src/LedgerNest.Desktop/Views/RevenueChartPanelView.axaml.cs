using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the reusable XAML frame for the revenue chart and its export actions.</summary>
public sealed partial class RevenueChartPanelView : UserControl
{
    public RevenueChartPanelView()
    {
        InitializeComponent();
    }

    public RevenueChartPanelView(string subtitle, Control actions, Control chart, Control legend)
        : this()
    {
        SubtitleText.Text = subtitle;
        ActionsHost.Content = actions;
        ChartHost.Content = chart;
        LegendHost.Content = legend;
    }
}
