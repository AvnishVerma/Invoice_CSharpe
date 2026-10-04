using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class RevenueChartPanelView : UserControl
{
    public RevenueChartPanelView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => DrawChart();
        DataContextChanged += (_, _) => DrawChart();
        ReportChartTheme.Apply(Chart);
    }

    // ScottPlot's data-series API draws into the chart declared in AXAML; it does not create Avalonia controls.
    private void DrawChart()
    {
        if (DataContext is not RevenueReportViewModel model) return;
        var report = model.Report;
        Chart.Plot.Clear();
        if (report.Months.Length == 0) return;
        for (var index = 0; index < report.Months.Length; index++)
        {
            var x = index + 1d;
            var billed = Chart.Plot.Add.Bar(x - .018, (double)report.Months[index].Billed);
            billed.Color = ScottPlot.Color.FromHex("#3B82F6"); billed.Bars[0].Size = .014;
            var collected = Chart.Plot.Add.Bar(x, (double)report.Months[index].Collected);
            collected.Color = ScottPlot.Color.FromHex("#22C55E"); collected.Bars[0].Size = .014;
            var profit = Chart.Plot.Add.Bar(x + .018, (double)report.Months[index].Profit);
            profit.Color = ScottPlot.Color.FromHex("#7C3AED"); profit.Bars[0].Size = .014;
        }
        Chart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            Enumerable.Range(1, report.Months.Length).Select(value => (double)value).ToArray(),
            report.Months.Select(month => month.Month.ToString("MMM yy")).ToArray());
        Chart.Plot.Axes.SetLimitsX(.4, Math.Max(1, report.Months.Length) + .6);
        Chart.Plot.Axes.Left.Min = Math.Min(0, (double)report.Months.Min(month => month.Profit) * 1.15);
        Chart.Plot.Grid.XAxisStyle.IsVisible = false;
        Chart.Plot.Legend.IsVisible = false;
        ReportChartTheme.Apply(Chart);
        Chart.Refresh();
    }
}
