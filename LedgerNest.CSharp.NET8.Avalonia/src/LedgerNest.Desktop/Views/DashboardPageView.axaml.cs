using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class DashboardPageView : UserControl
{
    // Performs the dashboard page view initialization action for this screen or workflow.
    public DashboardPageView()
    {
        InitializeComponent();
    }

    // Performs the dashboard page view data context assignment action for this screen or workflow.
    public DashboardPageView(DashboardPageModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

public sealed class DashboardPageModel
{
    public ObservableCollection<DashboardTileModel> Tiles { get; } = [];
    public ObservableCollection<DashboardInvoiceModel> RecentInvoices { get; } = [];
    public bool HasNoInvoices => RecentInvoices.Count == 0;
    public ICommand RefreshCommand { get; }

    // Performs the dashboard page model initialization action for this screen or workflow.
    public DashboardPageModel(IEnumerable<DashboardTileModel> tiles, IEnumerable<DashboardInvoiceModel> recentInvoices, Action refresh)
    {
        foreach (var tile in tiles) Tiles.Add(tile);
        foreach (var invoice in recentInvoices) RecentInvoices.Add(invoice);
        RefreshCommand = new RelayCommand(refresh);
    }
}

public sealed record DashboardTileModel(string Label, string Value, string Icon, IBrush Accent, IBrush IconBackground);

public sealed class DashboardInvoiceModel
{
    public int Number { get; init; }
    public string InvoiceTitle { get; init; } = "";
    public string CustomerLine { get; init; } = "";
    public string DateLine { get; init; } = "";
    public string TotalText { get; init; } = "";
    public string Status { get; init; } = "";
    public IBrush StatusBrush { get; init; } = Brushes.Gray;
    public IBrush StatusBackground { get; init; } = Brushes.Transparent;
    public ICommand ViewCommand { get; init; } = new RelayCommand(() => { });
    public ICommand EditCommand { get; init; } = new RelayCommand(() => { });
    public ICommand CloneCommand { get; init; } = new RelayCommand(() => { });
    public ICommand PdfCommand { get; init; } = new RelayCommand(() => { });
    public ICommand PrintCommand { get; init; } = new RelayCommand(() => { });
    public ICommand PaymentCommand { get; init; } = new RelayCommand(() => { });
    public ICommand DeleteCommand { get; init; } = new RelayCommand(() => { });
}
