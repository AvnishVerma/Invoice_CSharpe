using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceStatusReportView : UserControl
{
    // Initializes the invoice status report for the AXAML loader.
    public InvoiceStatusReportView()
    {
        InitializeComponent();
    }

    // Initializes the invoice status report with calculated data and export actions.
    public InvoiceStatusReportView(InvoiceStatusReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides date, status-filter, KPI, and table state for the invoice status report.
public sealed class InvoiceStatusReportViewModel : INotifyPropertyChanged
{
    private readonly InvoiceStatusRowSnapshot[] allRows;
    private bool monthAndYear;
    private string selectedStatus = "All";
    private string selectedMonth;
    private int selectedYear;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string[] Months { get; } = Enumerable.Range(1, 12).Select(month => new DateTime(2000, month, 1).ToString("MMMM")).ToArray();
    public int[] Years { get; }
    public bool IsLastThirtyDays => !monthAndYear;
    public bool IsMonthAndYear => monthAndYear;
    public string SelectedStatus => selectedStatus;
    public bool IsAllSelected => selectedStatus == "All";
    public bool IsPaidSelected => selectedStatus == "Paid";
    public bool IsPartialSelected => selectedStatus == "Partial";
    public bool IsUnpaidSelected => selectedStatus == "Unpaid";
    public bool IsOverdueSelected => selectedStatus == "Overdue";
    public ICommand ShowLastThirtyDaysCommand { get; }
    public ICommand ShowMonthAndYearCommand { get; }
    public ICommand SelectStatusCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportPdfCommand { get; }

    public string SelectedMonth
    {
        get => selectedMonth;
        set
        {
            if (selectedMonth == value || string.IsNullOrWhiteSpace(value)) return;
            selectedMonth = value;
            Refresh();
        }
    }

    public int SelectedYear
    {
        get => selectedYear;
        set
        {
            if (selectedYear == value) return;
            selectedYear = value;
            Refresh();
        }
    }

    public InvoiceStatusRowViewModel[] PeriodRows => GetPeriodRows().Select((row, index) => InvoiceStatusRowViewModel.From(row, index + 1)).ToArray();
    public InvoiceStatusRowViewModel[] VisibleRows => FilterStatus(GetPeriodRows()).Select((row, index) => InvoiceStatusRowViewModel.From(row, index + 1)).ToArray();
    public int TotalCount => GetPeriodRows().Length;
    public int PaidCount => GetPeriodRows().Count(row => row.Status == "Paid");
    public int PartialCount => GetPeriodRows().Count(row => row.Status == "Partial");
    public int UnpaidCount => GetPeriodRows().Count(row => row.Status == "Unpaid");
    public int OverdueCount => GetPeriodRows().Count(row => row.IsOverdue);
    public string AllFilterLabel => $"All ({TotalCount})";
    public string PaidFilterLabel => $"Paid ({PaidCount})";
    public string PartialFilterLabel => $"Partial ({PartialCount})";
    public string UnpaidFilterLabel => $"Unpaid ({UnpaidCount})";
    public string OverdueFilterLabel => $"Overdue ({OverdueCount})";
    public bool HasNoRows => VisibleRows.Length == 0;
    public string DateRangeText
    {
        get
        {
            var start = monthAndYear
                ? new DateTime(SelectedYear, Array.IndexOf(Months, SelectedMonth) + 1, 1)
                : DateTime.Today.AddDays(-29);
            var end = monthAndYear ? start.AddMonths(1).AddDays(-1) : DateTime.Today;
            return $"ⓘ  Showing invoices dated {start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
        }
    }
    public string RangeText => VisibleRows.Length == 0 ? "Rows per page:  10      0 items" : $"Rows per page:  10      1 – {VisibleRows.Length} of {VisibleRows.Length}";

    // Creates the interactive report state and commands from saved invoice rows.
    public InvoiceStatusReportViewModel(InvoiceStatusReportSnapshot report, Func<Task> exportCsv, Func<Task> exportPdf)
    {
        allRows = report.Invoices;
        selectedMonth = DateTime.Today.ToString("MMMM");
        selectedYear = DateTime.Today.Year;
        Years = allRows.Select(row => row.Date.Year).Append(DateTime.Today.Year).Distinct().OrderByDescending(year => year).ToArray();
        ShowLastThirtyDaysCommand = new RelayCommand(() => SetPeriod(false));
        ShowMonthAndYearCommand = new RelayCommand(() => SetPeriod(true));
        SelectStatusCommand = new RelayCommand<string>(status => SetStatus(status ?? "All"));
        ExportCsvCommand = new AsyncRelayCommand(exportCsv);
        ExportPdfCommand = new AsyncRelayCommand(exportPdf);
    }

    // Switches between rolling thirty-day and selected calendar-month periods.
    private void SetPeriod(bool useMonthAndYear)
    {
        if (monthAndYear == useMonthAndYear) return;
        monthAndYear = useMonthAndYear;
        OnPropertyChanged(nameof(IsLastThirtyDays));
        OnPropertyChanged(nameof(IsMonthAndYear));
        Refresh();
    }

    // Applies one payment-status or overdue filter to the invoice table.
    private void SetStatus(string status)
    {
        if (selectedStatus == status) return;
        selectedStatus = status;
        OnPropertyChanged(nameof(SelectedStatus));
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsPaidSelected));
        OnPropertyChanged(nameof(IsPartialSelected));
        OnPropertyChanged(nameof(IsUnpaidSelected));
        OnPropertyChanged(nameof(IsOverdueSelected));
        OnPropertyChanged(nameof(VisibleRows));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(RangeText));
    }

    // Returns invoice rows inside the currently selected date period.
    private InvoiceStatusRowSnapshot[] GetPeriodRows()
    {
        if (!monthAndYear) return allRows.Where(row => row.Date >= DateTime.Today.AddDays(-29) && row.Date <= DateTime.Today).ToArray();
        var month = Array.IndexOf(Months, SelectedMonth) + 1;
        return allRows.Where(row => row.Date.Year == SelectedYear && row.Date.Month == month).ToArray();
    }

    // Returns period rows matching the selected payment-status chip.
    private InvoiceStatusRowSnapshot[] FilterStatus(InvoiceStatusRowSnapshot[] rows) => selectedStatus switch
    {
        "Paid" or "Partial" or "Unpaid" => rows.Where(row => row.Status == selectedStatus).ToArray(),
        "Overdue" => rows.Where(row => row.IsOverdue).ToArray(),
        _ => rows
    };

    // Notifies the UI that period-dependent values have changed.
    private void Refresh()
    {
        OnPropertyChanged(nameof(SelectedMonth));
        OnPropertyChanged(nameof(SelectedYear));
        OnPropertyChanged(nameof(DateRangeText));
        OnPropertyChanged(nameof(PeriodRows));
        OnPropertyChanged(nameof(VisibleRows));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(PaidCount));
        OnPropertyChanged(nameof(PartialCount));
        OnPropertyChanged(nameof(UnpaidCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(AllFilterLabel));
        OnPropertyChanged(nameof(PaidFilterLabel));
        OnPropertyChanged(nameof(PartialFilterLabel));
        OnPropertyChanged(nameof(UnpaidFilterLabel));
        OnPropertyChanged(nameof(OverdueFilterLabel));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(RangeText));
    }

    // Raises a property-change notification for one presentation property.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

// Provides one formatted invoice row and status badge colors.
public sealed record InvoiceStatusRowViewModel(
    int Index,
    string Date,
    string InvoiceId,
    string Customer,
    string Total,
    string Paid,
    string Outstanding,
    string Status,
    string StatusBackground,
    string StatusForeground)
{
    // Converts one calculated invoice status row into display-ready values.
    public static InvoiceStatusRowViewModel From(InvoiceStatusRowSnapshot row, int index) => new(
        index,
        row.Date.ToString("dd/MM/yyyy"),
        row.InvoiceId,
        row.Customer,
        Money(row.Total),
        Money(row.Paid),
        row.Outstanding <= .005m ? "—" : Money(row.Outstanding),
        row.Status,
        row.Status switch { "Paid" => "#DDF3E6", "Partial" => "#FFF0D8", _ => "#E8E8F1" },
        row.Status switch { "Paid" => "#159447", "Partial" => "#E18A00", _ => "#526780" });

    private static string Money(decimal value) => CurrencyDisplay.Format(value, "N2");
}
