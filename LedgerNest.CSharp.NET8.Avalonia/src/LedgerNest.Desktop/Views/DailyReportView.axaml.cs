using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class DailyReportView : UserControl
{
    // Initializes the daily report for the AXAML loader.
    public DailyReportView()
    {
        InitializeComponent();
    }

    // Initializes the daily report with calculated rows and export actions.
    public DailyReportView(DailyReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides interactive period selection and formatted daily sales rows.
public sealed class DailyReportViewModel : INotifyPropertyChanged
{
    private readonly DailySalesDaySnapshot[] allDays;
    private string mode = "Today";
    private string selectedMonth;
    private int selectedYear;
    private DateTimeOffset? customStart;
    private DateTimeOffset? customEnd;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string[] Months { get; } = Enumerable.Range(1, 12).Select(month => new DateTime(2000, month, 1).ToString("MMMM")).ToArray();
    public int[] Years { get; }
    public bool IsToday => mode == "Today";
    public bool IsLastThirtyDays => mode == "Last 30 days";
    public bool IsMonthAndYear => mode == "Month & Year";
    public bool IsCustomRange => mode == "Custom Range";
    public ICommand SelectModeCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportPdfCommand { get; }

    public string SelectedMonth
    {
        get => selectedMonth;
        set { if (selectedMonth == value || string.IsNullOrWhiteSpace(value)) return; selectedMonth = value; Refresh(); }
    }

    public int SelectedYear
    {
        get => selectedYear;
        set { if (selectedYear == value) return; selectedYear = value; Refresh(); }
    }

    public DateTimeOffset? CustomStart
    {
        get => customStart;
        set { if (customStart == value) return; customStart = value; Refresh(); }
    }

    public DateTimeOffset? CustomEnd
    {
        get => customEnd;
        set { if (customEnd == value) return; customEnd = value; Refresh(); }
    }

    public DailyReportRowViewModel[] Rows => GetDays().Select(DailyReportRowViewModel.From).ToArray();
    public bool HasNoRows => Rows.Length == 0;
    public int MissingCostItemCount => GetDays().Sum(day => day.MissingCostItemCount);
    public bool HasMissingCosts => MissingCostItemCount > 0;
    public string MissingCostMessage => MissingCostItemCount == 1
        ? "1 item sold in this period has no purchase price set — profit/margin is understated for that item until a purchase price is added to the product."
        : $"{MissingCostItemCount} items sold in this period have no purchase price set — profit/margin is understated for those items until purchase prices are added to the products.";
    public string RangeText => Rows.Length == 0 ? "Rows per page:  25      0 items" : $"Rows per page:  25      1 – {Rows.Length} of {Rows.Length}";

    // Creates the period controls, daily rows, warnings, and export commands.
    public DailyReportViewModel(DailySalesReportSnapshot report, Func<Task> exportCsv, Func<Task> exportPdf)
    {
        allDays = report.Days;
        selectedMonth = DateTime.Today.ToString("MMMM");
        selectedYear = DateTime.Today.Year;
        Years = allDays.Select(day => day.Date.Year).Append(DateTime.Today.Year).Distinct().OrderByDescending(year => year).ToArray();
        customStart = allDays.Length == 0 ? DateTime.Today : allDays.Min(day => day.Date);
        customEnd = allDays.Length == 0 ? DateTime.Today : allDays.Max(day => day.Date);
        SelectModeCommand = new RelayCommand<string>(selected => SetMode(selected ?? "Today"));
        ExportCsvCommand = new AsyncRelayCommand(exportCsv);
        ExportPdfCommand = new AsyncRelayCommand(exportPdf);
    }

    // Changes the active date-filter mode and refreshes the report rows.
    private void SetMode(string selected)
    {
        if (mode == selected) return;
        mode = selected;
        OnPropertyChanged(nameof(IsToday));
        OnPropertyChanged(nameof(IsLastThirtyDays));
        OnPropertyChanged(nameof(IsMonthAndYear));
        OnPropertyChanged(nameof(IsCustomRange));
        Refresh();
    }

    // Returns daily totals inside the active date selection.
    private DailySalesDaySnapshot[] GetDays()
    {
        DateTime start;
        DateTime end;
        if (IsToday) start = end = DateTime.Today;
        else if (IsLastThirtyDays) { start = DateTime.Today.AddDays(-29); end = DateTime.Today; }
        else if (IsMonthAndYear)
        {
            start = new DateTime(SelectedYear, Array.IndexOf(Months, SelectedMonth) + 1, 1);
            end = start.AddMonths(1).AddDays(-1);
        }
        else
        {
            start = CustomStart?.Date ?? DateTime.Today;
            end = CustomEnd?.Date ?? start;
            if (end < start) (start, end) = (end, start);
        }
        return allDays.Where(day => day.Date >= start && day.Date <= end).OrderBy(day => day.Date).ToArray();
    }

    // Notifies the UI that all period-dependent values have changed.
    private void Refresh()
    {
        OnPropertyChanged(nameof(SelectedMonth));
        OnPropertyChanged(nameof(SelectedYear));
        OnPropertyChanged(nameof(CustomStart));
        OnPropertyChanged(nameof(CustomEnd));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(MissingCostItemCount));
        OnPropertyChanged(nameof(HasMissingCosts));
        OnPropertyChanged(nameof(MissingCostMessage));
        OnPropertyChanged(nameof(RangeText));
    }

    // Raises a property-change notification for one presentation property.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

// Provides one formatted daily sales and profit table row.
public sealed record DailyReportRowViewModel(string Date, int InvoiceCount, string Sales, string Cogs, string Profit, string Margin)
{
    // Converts one calculated day into display-ready values.
    public static DailyReportRowViewModel From(DailySalesDaySnapshot day)
    {
        var margin = day.Sales == 0 ? 0 : day.Profit * 100 / day.Sales;
        return new(day.Date.ToString("dd/MM/yyyy"), day.InvoiceCount, Money(day.Sales), Money(day.Cogs), Money(day.Profit), $"{margin:0.#}%");
    }

    private static string Money(decimal value) => CurrencyDisplay.Format(value, "N2");
}
