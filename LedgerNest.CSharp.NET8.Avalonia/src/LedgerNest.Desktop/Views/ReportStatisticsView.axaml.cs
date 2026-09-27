using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

/// <summary>Displays report KPI cards through a responsive XAML item template.</summary>
public sealed partial class ReportStatisticsView : UserControl
{
    public ObservableCollection<ReportStatisticModel> Statistics { get; } = [];

    public ReportStatisticsView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ReportStatisticsView(IEnumerable<ReportStatisticModel> statistics)
        : this()
    {
        foreach (var statistic in statistics) Statistics.Add(statistic);
    }
}

public sealed record ReportStatisticModel(string Label, string Value, string Icon, IBrush Accent, IBrush IconBackground);
