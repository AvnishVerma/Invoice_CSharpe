using Avalonia.Styling;
using ScottPlot.Avalonia;

namespace LedgerNest.Desktop.Views;

internal static class ReportChartTheme
{
    internal static void Apply(AvaPlot chart)
    {
        void Update()
        {
            var dark = chart.ActualThemeVariant == ThemeVariant.Dark;
            var background = ScottPlot.Color.FromHex(dark ? "#202B36" : "#FBF5FF");
            chart.Plot.FigureBackground.Color = background;
            chart.Plot.DataBackground.Color = background;
            chart.Plot.Axes.Color(ScottPlot.Color.FromHex(dark ? "#BBC5D0" : "#666666"));
            chart.Refresh();
        }
        chart.ActualThemeVariantChanged += (_, _) => Update();
        Update();
    }
}
