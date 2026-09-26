using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using LedgerNest.Desktop.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? dbFactory;
    private readonly string? databasePath;
    public static readonly string[] Routes = ["Dashboard", "New Invoice", "Invoices", "Quotations", "Receipts", "Customers", "Products", "Reports", "Settings"];
    [ObservableProperty] private string title = "Dashboard";
    [ObservableProperty] private bool sidebarExpanded = true;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string themeMode = "Light";
    [ObservableProperty] private string language = "English";
    [ObservableProperty] private string updateManifestUrl = "";
    [ObservableProperty] private string updateStatus = "Update checks are not configured.";
    [ObservableProperty] private string latestUpdateVersion = "";
    [ObservableProperty] private string latestUpdateNotes = "";
    [ObservableProperty] private string latestUpdateDownloadUrl = "";
    public HashSet<Guid> DeletedRecords { get; } = [];
    public ObservableCollection<UiRecord> Customers { get; } = [];
    public ObservableCollection<UiRecord> Products { get; } = [];
    public ObservableCollection<UiRecord> Users { get; } = [];
    public ObservableCollection<UiRecord> Invoices { get; } = [];
    public IEnumerable<UiRecord> ActiveInvoices => Invoices.Where(i => i["Type"] == "Invoice" && !DeletedRecords.Contains(i.Id));
    public ObservableCollection<UiRecord> Payments { get; } = [];
    public string PendingInvoiceCustomerFilter { get; private set; } = "";
    public string PendingReportCustomerFilter { get; private set; } = "";
    public ObservableCollection<InvoiceLineViewModel> Lines { get; } = [];
    public ObservableCollection<AppNotification> Notifications { get; } = [];
    public int UnreadNotificationCount => Notifications.Count(notification => !notification.IsRead);
    public Dictionary<string, FormSection[]> Settings { get; } = FormCatalog.Settings();
    public ObservableCollection<FormField[]> UpiAccounts { get; } = [];
    public ObservableCollection<FormField[]> BankAccounts { get; } = [];
    public FormField[] InvoiceCustomer { get; } = FormCatalog.Customer();
    public FormField[] InvoiceDetails { get; } = [new("Type", "Invoice", "choice", ["Invoice", "Quotation", "Receipt"]), new("Order Date", DateTime.Today.ToString("yyyy-MM-dd"), "date"), new("Due Date", "", "date"), new("GST title", "Invoice", "choice", ["Invoice", "Tax Invoice", "Bill of Supply", "Invoice-cum-Bill of Supply", "Cash Bill", "Credit Note", "Debit Note", "Revised Invoice"]), new("PDF invoice number")];
    public FormField HideInvoiceNumber { get; } = new("Hide invoice number in PDF", "false", "toggle");
    public FormField InterState { get; } = new("Inter-state", "false", "toggle");
    public FormField[] InvoiceOptions { get; } = [new("Discount Type", "None", "choice", ["None", "Amount", "Percentage"]), new("Discount Value", "0", "number"), new("Notes", kind: "multiline"), new("Tax Mode", "Per Item", "choice", ["Per Item", "Global", "No Tax"]), new("Tax Rate (%)", "18", "number")];
    public ObservableCollection<FormField[]> AdditionalCosts { get; } = [];
    public CalculatedInvoiceTotals Totals => InvoiceTotalsCalculator.Calculate(
        Lines.Select(l => new InvoiceLineInput(l.Price, l.Quantity, l.Discount, l.DiscountPerUnit, l.ExtraCost, l.TaxRate, l.PriceIncludesTax)),
        InvoiceOptions[3].Value switch { "Global" => InvoiceTaxMode.Global, "No Tax" => InvoiceTaxMode.None, _ => InvoiceTaxMode.PerItem },
        InvoiceOptions[4].Number, AdditionalCosts.Sum(c => c[1].Number),
        InvoiceOptions[0].Value switch { "Amount" => InvoiceDiscountKind.Amount, "Percentage" => InvoiceDiscountKind.Percent, _ => InvoiceDiscountKind.None }, InvoiceOptions[1].Number);
    public event Action? InvoiceChanged;
    public MainWindowViewModel(IDbContextFactory<LedgerNestDbContext>? dbFactory = null, string? databasePath = null, ILicenseService? licenseService = null)
    {
        this.dbFactory = dbFactory;
        this.databasePath = databasePath;
        this.licenseService = licenseService ?? new UnavailableLicenseService();
        RefreshLicense();
        foreach (var option in InvoiceOptions) option.PropertyChanged += (_, _) => InvoiceChanged?.Invoke();
        AdditionalCosts.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (FormField[] fields in e.NewItems) fields[1].PropertyChanged += (_, _) => InvoiceChanged?.Invoke();
            InvoiceChanged?.Invoke();
        };
        Lines.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (InvoiceLineViewModel line in e.NewItems) line.PropertyChanged += LineChanged;
            if (e.OldItems != null) foreach (InvoiceLineViewModel line in e.OldItems) line.PropertyChanged -= LineChanged;
            InvoiceChanged?.Invoke();
        };
        LoadPersistedRecords();
        LoadPersistedSettings();
        savedStartingNumber = InvoiceSetting("Starting Number").Value;
        LoadPaymentAccounts();
        LoadThemeMode();
        LoadLanguage();
        ApplyDefaultCustomer();
    }
    // Performs the line changed action for this screen or workflow.
    private void LineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvoiceChanged?.Invoke();
    [RelayCommand] private void Navigate(string route) { if (Routes.Contains(route)) { Title = route; Status = ""; } }
    [RelayCommand] private void ToggleSidebar() => SidebarExpanded = !SidebarExpanded;

    // Performs the customer invoice filter queue action for customer management row actions.
    public void QueueInvoiceCustomerFilter(string customerName) => PendingInvoiceCustomerFilter = customerName;

    // Performs the customer report filter queue action for customer management payment actions.
    public void QueueCustomerReportFilter(string customerName) => PendingReportCustomerFilter = customerName;

    // Performs the customer invoice navigation action for customer management row actions.
    public void OpenInvoicesForCustomer(string customerName)
    {
        QueueInvoiceCustomerFilter(customerName);
        NavigateCommand.Execute("Invoices");
    }

    // Performs the customer report navigation action for customer management payment actions.
    public void OpenCustomerReport(string customerName)
    {
        QueueCustomerReportFilter(customerName);
        NavigateCommand.Execute("Reports");
    }

    // Persists the publisher-controlled endpoint used for update checks.
    public bool SaveUpdateManifestUrl(string? value)
    {
        var url = value?.Trim() ?? "";
        if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            Status = "The update manifest URL must use HTTPS.";
            return false;
        }

        UpdateManifestUrl = url;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            SetSetting(db, "updates.manifest_url", url);
            db.SaveChanges();
        }
        Status = url.Length == 0 ? "Update checks disabled." : "Update channel saved.";
        return true;
    }

    // Applies an update result to the persistent view-model state and notification center.
    public void ApplyUpdateCheck(UpdateCheckResult result)
    {
        UpdateStatus = result.Message;
        LatestUpdateVersion = result.Manifest?.Version ?? "";
        LatestUpdateNotes = result.Manifest?.ReleaseNotes ?? "";
        LatestUpdateDownloadUrl = result.Manifest?.DownloadUrl ?? "";
        if (result.IsUpdateAvailable && result.Manifest != null)
            PublishNotification("Update available", $"Version {result.Manifest.Version} is ready to download.", NotificationType.Update);
    }

    // Adds an in-app notification and updates the unread badge count.
    public void PublishNotification(string title, string message, NotificationType type = NotificationType.Information)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Notifications.Insert(0, new AppNotification(title, message.Trim(), type, DateTimeOffset.Now));
        OnPropertyChanged(nameof(UnreadNotificationCount));
    }

    // Marks all in-app notifications as read.
    public void MarkNotificationsRead()
    {
        foreach (var notification in Notifications) notification.IsRead = true;
        OnPropertyChanged(nameof(UnreadNotificationCount));
    }

    // Performs the pending invoice customer filter consumption action for invoice management.
    public string ConsumePendingInvoiceCustomerFilter()
    {
        var value = PendingInvoiceCustomerFilter;
        PendingInvoiceCustomerFilter = "";
        return value;
    }

    // Performs the pending report customer filter consumption action for customer reports.
    public string ConsumePendingReportCustomerFilter()
    {
        var value = PendingReportCustomerFilter;
        PendingReportCustomerFilter = "";
        return value;
    }




}
