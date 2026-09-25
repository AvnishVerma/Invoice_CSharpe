using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ReceivablesReportView : UserControl
{
    // Initializes the receivables report view for the AXAML loader.
    public ReceivablesReportView()
    {
        InitializeComponent();
    }

    // Initializes the report with calculated receivables data and renders its donut chart.
    public ReceivablesReportView(ReceivablesReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        ConfigurePaymentStatusChart(model);
    }

    // Configures the ScottPlot donut from the real paid, partial, and unpaid invoice counts.
    private void ConfigurePaymentStatusChart(ReceivablesReportViewModel model)
    {
        var slices = new[]
        {
            CreateSlice(model.PaidCount, model.InvoiceCount, "Paid", "#22C55E"),
            CreateSlice(model.PartialCount, model.InvoiceCount, "Partial", "#F59E0B"),
            CreateSlice(model.UnpaidCount, model.InvoiceCount, "Unpaid", "#EF4444")
        }.Where(slice => slice.Value > 0).ToList();

        if (slices.Count == 0)
            slices.Add(new ScottPlot.PieSlice { Value = 1, FillColor = ScottPlot.Color.FromHex("#E5E7EB") });

        var pie = PaymentStatusChart.Plot.Add.Pie(slices);
        pie.DonutFraction = .55;
        pie.SliceLabelDistance = .7;
        PaymentStatusChart.Plot.Axes.Frameless();
        PaymentStatusChart.Plot.Grid.XAxisStyle.IsVisible = false;
        PaymentStatusChart.Plot.Grid.YAxisStyle.IsVisible = false;
        PaymentStatusChart.Plot.Legend.IsVisible = false;
        ReportChartTheme.Apply(PaymentStatusChart);
        PaymentStatusChart.Refresh();
    }

    // Creates one colored donut slice with its percentage label.
    private static ScottPlot.PieSlice CreateSlice(int count, int total, string name, string color) => new()
    {
        Value = count,
        Label = total == 0 ? "" : $"{count * 100d / total:0.#}%",
        LegendText = name,
        FillColor = ScottPlot.Color.FromHex(color)
    };
}

// Exposes formatted receivables data and export actions to the AXAML report view.
public sealed class ReceivablesReportViewModel
{
    public int InvoiceCount { get; }
    public int PaidCount { get; }
    public int PartialCount { get; }
    public int UnpaidCount { get; }
    public string PaidLegend => Legend("Paid", PaidCount);
    public string PartialLegend => Legend("Partial", PartialCount);
    public string UnpaidLegend => Legend("Unpaid", UnpaidCount);
    public string InvoiceCountText => $"{InvoiceCount} total {(InvoiceCount == 1 ? "invoice" : "invoices")}";
    public ReceivableAgingRowViewModel[] AgingRows { get; }
    public ReceivableAgingRowViewModel AgingTotals { get; }
    public AgedReceivableRowViewModel[] AgedRows { get; }
    public bool HasNoAgingRows => AgingRows.Length == 0;
    public bool HasNoAgedRows => AgedRows.Length == 0;
    public string AgedTitle => $"Aged Receivables ({AgedRows.Length})";
    public string AgingRange => Range(AgingRows.Length);
    public string AgedRange => Range(AgedRows.Length);
    public string AgedTotal => Money(AgedRows.Sum(row => row.Value));
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportPdfCommand { get; }

    // Maps the calculated report snapshot into display rows and asynchronous export commands.
    public ReceivablesReportViewModel(ReceivablesReportSnapshot report, Func<Task> exportCsv, Func<Task> exportPdf)
    {
        InvoiceCount = report.InvoiceCount;
        PaidCount = report.PaidCount;
        PartialCount = report.PartialCount;
        UnpaidCount = report.UnpaidCount;
        AgingRows = report.AgingSummaries.Select(ReceivableAgingRowViewModel.From).ToArray();
        AgingTotals = ReceivableAgingRowViewModel.From(new ReceivableAgingSummarySnapshot(
            "Total",
            report.AgingSummaries.Sum(row => row.Current),
            report.AgingSummaries.Sum(row => row.Days0To30),
            report.AgingSummaries.Sum(row => row.Days31To60),
            report.AgingSummaries.Sum(row => row.Days61To90),
            report.AgingSummaries.Sum(row => row.Days90Plus),
            report.AgingSummaries.Sum(row => row.NoDueDate)));
        AgedRows = report.AgedReceivables.Select(AgedReceivableRowViewModel.From).ToArray();
        ExportCsvCommand = new AsyncRelayCommand(exportCsv);
        ExportPdfCommand = new AsyncRelayCommand(exportPdf);
    }

    // Formats one status legend with count and percentage.
    private string Legend(string name, int count) => $"{name}  {count}  ({(InvoiceCount == 0 ? 0 : count * 100d / InvoiceCount):0.0}%)";

    // Formats the common report paging range used below each table.
    private static string Range(int count) => count == 0 ? "Rows per page:  10      0 items" : $"Rows per page:  10      1 – {count} of {count}";

    // Formats report currency values consistently with the reference layout.
    internal static string Money(decimal value) => value == 0 ? "—" : $"Rs. {value:0.00}";
}

// Provides the formatted columns for one customer aging summary row.
public sealed record ReceivableAgingRowViewModel(
    string Customer,
    string Current,
    string Days0To30,
    string Days31To60,
    string Days61To90,
    string Days90Plus,
    string NoDueDate,
    string Total)
{
    // Creates a display row from calculated customer aging amounts.
    public static ReceivableAgingRowViewModel From(ReceivableAgingSummarySnapshot row) => new(
        row.Customer,
        ReceivablesReportViewModel.Money(row.Current),
        ReceivablesReportViewModel.Money(row.Days0To30),
        ReceivablesReportViewModel.Money(row.Days31To60),
        ReceivablesReportViewModel.Money(row.Days61To90),
        ReceivablesReportViewModel.Money(row.Days90Plus),
        ReceivablesReportViewModel.Money(row.NoDueDate),
        ReceivablesReportViewModel.Money(row.Total));
}

// Provides the formatted columns for one outstanding invoice row.
public sealed record AgedReceivableRowViewModel(
    string Customer,
    string InvoiceId,
    string Outstanding,
    string DaysOverdue,
    string Bucket,
    decimal Value)
{
    // Creates a display row from one calculated aged receivable.
    public static AgedReceivableRowViewModel From(AgedReceivableSnapshot row) => new(
        row.Customer,
        row.InvoiceId,
        ReceivablesReportViewModel.Money(row.Outstanding),
        row.DaysOverdue?.ToString() ?? "—",
        row.Bucket switch
        {
            ReceivableAgingBucket.Current => "Current",
            ReceivableAgingBucket.Days0To30 => "0–30 Days",
            ReceivableAgingBucket.Days31To60 => "31–60 Days",
            ReceivableAgingBucket.Days61To90 => "61–90 Days",
            ReceivableAgingBucket.Days90Plus => "90+ Days",
            _ => "No Due Date"
        },
        row.Outstanding);
}
