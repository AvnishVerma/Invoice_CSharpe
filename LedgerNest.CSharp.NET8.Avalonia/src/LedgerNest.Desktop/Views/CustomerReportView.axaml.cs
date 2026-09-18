using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class CustomerReportView : UserControl
{
    // Initializes the customer report view for the AXAML loader.
    public CustomerReportView()
    {
        InitializeComponent();
    }

    // Initializes the customer report with its presentation model.
    public CustomerReportView(CustomerReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Exposes customer overview and statement state to the AXAML report view.
public sealed class CustomerReportViewModel : INotifyPropertyChanged
{
    private bool isStatement;
    private CustomerStatementOptionViewModel? selectedCustomer;

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsOverview => !isStatement;
    public bool IsStatement => isStatement;
    public CustomerRevenueRowViewModel[] RevenueRows { get; }
    public CustomerStatementOptionViewModel[] CustomerOptions { get; }
    public bool HasNoRevenueRows => RevenueRows.Length == 0;
    public string RevenueTitle => $"Top {RevenueRows.Length} Customers by Revenue";
    public string RevenueRange => RevenueRows.Length == 0 ? "Rows per page:  10      0 items" : $"Rows per page:  10      1 – {RevenueRows.Length} of {RevenueRows.Length}";
    public ICommand ShowOverviewCommand { get; }
    public ICommand ShowStatementCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportPdfCommand { get; }

    public CustomerStatementOptionViewModel? SelectedCustomer
    {
        get => selectedCustomer;
        set
        {
            if (ReferenceEquals(selectedCustomer, value)) return;
            selectedCustomer = value;
            NotifyStatementChanged();
        }
    }

    public string SelectedCustomerName => SelectedCustomer?.Name ?? "Select customer";
    public string SelectedInvoiced => Money(SelectedCustomer?.Billed ?? 0);
    public string SelectedPaid => Money(SelectedCustomer?.Collected ?? 0);
    public string SelectedClosing => Money(SelectedCustomer?.Outstanding ?? 0);
    public string SelectedOverdue => Money(SelectedCustomer?.Overdue ?? 0);
    public CustomerStatementRowViewModel[] StatementRows => SelectedCustomer?.Transactions ?? [];
    public bool HasNoStatementRows => StatementRows.Length == 0;

    // Maps customer report calculations to overview rows, statement choices, tabs, and exports.
    public CustomerReportViewModel(CustomerReportSnapshot report, string selectedCustomerName, Func<Task> exportCsv, Func<Task> exportPdf)
    {
        var maximum = Math.Max(1m, report.RevenueCustomers.Select(customer => customer.Billed).DefaultIfEmpty(0m).Max());
        RevenueRows = report.RevenueCustomers.Select((customer, index) => CustomerRevenueRowViewModel.From(customer, index + 1, maximum)).ToArray();
        CustomerOptions = report.StatementCustomers.Select(CustomerStatementOptionViewModel.From).ToArray();
        selectedCustomer = CustomerOptions.FirstOrDefault(customer => customer.Name.Equals(selectedCustomerName, StringComparison.OrdinalIgnoreCase))
            ?? CustomerOptions.FirstOrDefault();
        isStatement = !string.IsNullOrWhiteSpace(selectedCustomerName);
        ShowOverviewCommand = new RelayCommand(() => SetStatementVisible(false));
        ShowStatementCommand = new RelayCommand(() => SetStatementVisible(true));
        ExportCsvCommand = new AsyncRelayCommand(exportCsv);
        ExportPdfCommand = new AsyncRelayCommand(exportPdf);
    }

    // Switches between overview and statement layouts while retaining the selected customer.
    private void SetStatementVisible(bool value)
    {
        if (isStatement == value) return;
        isStatement = value;
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(IsStatement));
    }

    // Notifies all statement bindings after the customer selection changes.
    private void NotifyStatementChanged()
    {
        OnPropertyChanged(nameof(SelectedCustomer));
        OnPropertyChanged(nameof(SelectedCustomerName));
        OnPropertyChanged(nameof(SelectedInvoiced));
        OnPropertyChanged(nameof(SelectedPaid));
        OnPropertyChanged(nameof(SelectedClosing));
        OnPropertyChanged(nameof(SelectedOverdue));
        OnPropertyChanged(nameof(StatementRows));
        OnPropertyChanged(nameof(HasNoStatementRows));
    }

    // Raises a property-change notification for one presentation property.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Formats customer report currency values consistently.
    internal static string Money(decimal value) => $"Rs. {value:0.00}";
}

// Provides one customer ranking row for the overview chart and table.
public sealed record CustomerRevenueRowViewModel(
    int Index,
    string Name,
    string InvoiceCount,
    string Billed,
    string Collected,
    string Outstanding,
    decimal Value,
    decimal Maximum)
{
    // Creates a formatted revenue row from a calculated customer summary.
    public static CustomerRevenueRowViewModel From(CustomerReportCustomerSnapshot customer, int index, decimal maximum) => new(
        index,
        customer.Name,
        customer.InvoiceCount.ToString(),
        CustomerReportViewModel.Money(customer.Billed),
        CustomerReportViewModel.Money(customer.Collected),
        customer.Outstanding <= .005m ? "—" : CustomerReportViewModel.Money(customer.Outstanding),
        customer.Billed,
        maximum);
}

// Provides one selectable customer and its calculated statement values.
public sealed record CustomerStatementOptionViewModel(
    string Name,
    string Label,
    decimal Billed,
    decimal Collected,
    decimal Outstanding,
    decimal Overdue,
    CustomerStatementRowViewModel[] Transactions)
{
    // Creates a customer choice and formats all of its statement ledger rows.
    public static CustomerStatementOptionViewModel From(CustomerReportCustomerSnapshot customer) => new(
        customer.Name,
        $"{customer.Name} ({customer.InvoiceCount})",
        customer.Billed,
        customer.Collected,
        customer.Outstanding,
        customer.Overdue,
        customer.Transactions.Select((entry, index) => CustomerStatementRowViewModel.From(entry, index + 1)).ToArray());
}

// Provides one formatted customer statement transaction row.
public sealed record CustomerStatementRowViewModel(
    int Index,
    string Date,
    string Type,
    string TypeColor,
    string Reference,
    string Description,
    string Debit,
    string Credit,
    string Balance)
{
    // Creates a display row from one customer debit or credit transaction.
    public static CustomerStatementRowViewModel From(CustomerStatementTransactionSnapshot entry, int index) => new(
        index,
        entry.Date.ToString("dd/MM/yyyy"),
        entry.Type,
        entry.Type == "Payment" ? "#16A34A" : "#0D47A1",
        entry.Reference,
        entry.Description,
        entry.Debit == 0 ? "—" : CustomerReportViewModel.Money(entry.Debit),
        entry.Credit == 0 ? "—" : CustomerReportViewModel.Money(entry.Credit),
        CustomerReportViewModel.Money(entry.Balance));
}
