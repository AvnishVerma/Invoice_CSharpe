using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public enum ReportMetricKind { Blue, Green, Red, Purple, Sky, Violet }
public sealed record ReportStatisticModel(string Label, string Value, string Icon, ReportMetricKind Kind)
{
    public bool IsBlue => Kind == ReportMetricKind.Blue;
    public bool IsGreen => Kind == ReportMetricKind.Green;
    public bool IsRed => Kind == ReportMetricKind.Red;
    public bool IsPurple => Kind == ReportMetricKind.Purple;
    public bool IsSky => Kind == ReportMetricKind.Sky;
    public bool IsViolet => Kind == ReportMetricKind.Violet;
}
public sealed record ReportStatisticsModel(IReadOnlyList<ReportStatisticModel> Statistics);

public sealed record ReportCellModel(string Text, bool IsHeader, bool IsFirst, bool IsMoney, bool IsDanger);
public sealed record ReportRowModel(IReadOnlyList<ReportCellModel> Cells, bool IsHeader, bool IsEven);
public sealed class ReportTableViewModel
{
    public string Title { get; }
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public bool WrapInCard { get; }
    public IReadOnlyList<ReportRowModel> Rows { get; }
    public bool HasNoRows => Rows.Count == 0;
    public EmptyStateViewModel EmptyState { get; } = new("No data for this period", "", "receipt_long");
    public IAsyncRelayCommand ExportCsvCommand { get; }
    public IAsyncRelayCommand ExportPdfCommand { get; }
    public ReportTableViewModel(string title, string[][] rows, bool wrapInCard, Func<Task> csv, Func<Task> pdf)
    {
        Title = title;
        WrapInCard = wrapInCard;
        Rows = rows.Select((row, index) => new ReportRowModel(row.Select((value, column) =>
        {
            var money = index != 0 && value.Contains(CurrencyDisplay.Symbol());
            return new ReportCellModel(index == 0 ? value.ToUpperInvariant() : value, index == 0, column == 0,
                money, money && value.Contains("70"));
        }).ToArray(), index == 0, index % 2 == 0)).ToArray();
        ExportCsvCommand = new AsyncRelayCommand(csv);
        ExportPdfCommand = new AsyncRelayCommand(pdf);
    }
}

public sealed record SimpleReportViewModel(ReportStatisticsModel Statistics, ReportTableViewModel? Table,
    string InformationTitle, IReadOnlyList<string> Information)
{
    public bool HasTable => Table != null;
    public bool HasStatistics => Statistics.Statistics.Count > 0;
    public bool HasInformation => Information.Count > 0;
    public ReportInformationModel InformationContent { get; } = new(InformationTitle, Information);
}

public sealed record ReportInformationModel(string Title, IReadOnlyList<string> Details);
public sealed record ReportNoticeModel(string Icon, string Message);

public sealed record RevenueBreakdownRowModel(string Month, string Invoices, string Billed, string Collected,
    string Outstanding, string Cogs, string Profit, string Margin, bool IsTotal = false);

public sealed class RevenueReportViewModel
{
    public RevenueReportSnapshot Report { get; }
    public ReportStatisticsModel Statistics { get; }
    public bool HasMonths => Report.Months.Length > 0;
    public bool HasMissingCosts => Report.MissingCostItemCount > 0;
    public string MissingCostMessage { get; }
    public ReportNoticeModel Notice => new("warning_amber", MissingCostMessage);
    public EmptyStateViewModel EmptyChart { get; } = new("No invoice data for this period", "Create an invoice to populate the revenue trend.", "receipt_long");
    public string ChartSubtitle => $"{Report.InvoiceCount} invoices in period · {CurrencyDisplay.Code()}";
    public IReadOnlyList<RevenueBreakdownRowModel> Rows { get; }
    public IAsyncRelayCommand ExportCsvCommand { get; }
    public IAsyncRelayCommand ExportPdfCommand { get; }
    public RevenueReportViewModel(RevenueReportSnapshot report, Func<Task> csv, Func<Task> pdf)
    {
        Report = report;
        Statistics = new ReportStatisticsModel([
            new("Total Billed", Money(report.Billed), "receipt_long", ReportMetricKind.Blue),
            new("Total Collected", Money(report.Collected), "check_circle", ReportMetricKind.Green),
            new("Outstanding", Money(report.Outstanding), "schedule", ReportMetricKind.Red),
            new("Avg Invoice Value", Money(report.AverageInvoiceValue), "trending_up", ReportMetricKind.Purple),
            new("Total Profit", Money(report.TotalProfit), "savings", report.TotalProfit < 0 ? ReportMetricKind.Red : ReportMetricKind.Green),
            new("Realized Profit", Money(report.RealizedProfit), "payments", report.RealizedProfit < 0 ? ReportMetricKind.Red : ReportMetricKind.Green)
        ]);
        var count = report.MissingCostItemCount;
        MissingCostMessage = $"{count} {(count == 1 ? "item" : "items")} sold in this period {(count == 1 ? "has" : "have")} no purchase price set — profit/margin is understated for {(count == 1 ? "that item" : "those items")} until a purchase price is added to the product.";
        var rows = report.Months.Select(month => new RevenueBreakdownRowModel(month.Month.ToString("MMM yyyy"),
            month.InvoiceCount.ToString(), Money(month.Billed), Money(month.Collected), Money(month.Outstanding),
            Money(month.Cogs), Money(month.Profit), $"{month.MarginPercent:0.#}%")).ToList();
        if (HasMonths)
        {
            var revenue = report.Months.Sum(month => month.Profit + month.Cogs);
            var margin = revenue == 0 ? 0 : report.Months.Sum(month => month.Profit) * 100 / revenue;
            rows.Add(new("Total", report.Months.Sum(month => month.InvoiceCount).ToString(),
                Money(report.Months.Sum(month => month.Billed)), Money(report.Months.Sum(month => month.Collected)),
                Money(report.Months.Sum(month => month.Outstanding)), Money(report.Months.Sum(month => month.Cogs)),
                Money(report.Months.Sum(month => month.Profit)), $"{margin:0.#}%", true));
        }
        Rows = rows;
        ExportCsvCommand = new AsyncRelayCommand(csv);
        ExportPdfCommand = new AsyncRelayCommand(pdf);
    }
    private static string Money(decimal value) => CurrencyDisplay.Format(value, "0.00");
}
