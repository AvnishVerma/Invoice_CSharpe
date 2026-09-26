using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class StatisticsView : UserControl
{
    public static readonly StyledProperty<int> ColumnCountProperty = AvaloniaProperty.Register<StatisticsView, int>(nameof(ColumnCount), 1);
    public int ColumnCount { get => GetValue(ColumnCountProperty); set => SetValue(ColumnCountProperty, value); }
    private int count = 1;

    public StatisticsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => ColumnCount = Math.Clamp((int)((e.NewSize.Width + 12) / 182), 1, count);
    }

    public StatisticsView(IEnumerable<(string Label, string Value, string Subtitle, string Color)> values) : this()
    {
        var items = values.Select(s => new StatisticItem(s.Label, s.Value, s.Subtitle,
            s.Label.Contains("Customer") ? "\ue7fb" : s.Label.Contains("Product") ? "\ue1a1" : "\uef6e",
            new SolidColorBrush(Color.Parse(s.Color), .12))).ToArray();
        count = Math.Max(1, items.Length);
        ColumnCount = count;
        DataContext = items;
    }
}

public sealed record StatisticItem(string Label, string Value, string Subtitle, string Icon, IBrush IconBackground);
