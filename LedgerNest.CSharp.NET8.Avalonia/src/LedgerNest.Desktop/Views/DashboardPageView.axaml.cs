using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

// Provides the dashboard data and commands consumed by the AXAML dashboard view.
public sealed class DashboardPageModel : INotifyPropertyChanged
{
    private readonly Action<string> layoutChanged;
    private string layout = "Default";

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<DashboardTileModel> Tiles { get; } = [];
    public ObservableCollection<DashboardInvoiceModel> RecentInvoices { get; } = [];
    public bool HasNoInvoices => RecentInvoices.Count == 0;
    public bool ShowHero => Layout is not "Simple Feed" and not "Bento";
    public bool ShowTiles => Layout != "Simple Feed";
    public bool ShowFeedInvoices => Layout != "Classic";
    public bool ShowCompactInvoices => Layout == "Classic";
    public string LayoutTitle => $"Layout: {Layout}";
    public ICommand RefreshCommand { get; }
    public ICommand SelectLayoutCommand { get; }

    public string Layout
    {
        get => layout;
        private set
        {
            if (layout == value) return;
            layout = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHero));
            OnPropertyChanged(nameof(ShowTiles));
            OnPropertyChanged(nameof(ShowFeedInvoices));
            OnPropertyChanged(nameof(ShowCompactInvoices));
            OnPropertyChanged(nameof(LayoutTitle));
        }
    }

    // Performs the dashboard page model initialization action for this screen or workflow.
    public DashboardPageModel(IEnumerable<DashboardTileModel> tiles, IEnumerable<DashboardInvoiceModel> recentInvoices, Action refresh, string initialLayout = "Default", Action<string>? layoutChanged = null)
    {
        this.layoutChanged = layoutChanged ?? (_ => { });
        foreach (var tile in tiles) Tiles.Add(tile);
        foreach (var invoice in recentInvoices) RecentInvoices.Add(invoice);
        RefreshCommand = new RelayCommand(refresh);
        SelectLayoutCommand = new RelayCommand<string>(SelectLayout);
        layout = initialLayout;
    }

    // Performs the dashboard layout selection action for this screen or workflow.
    private void SelectLayout(string? selectedLayout)
    {
        if (string.IsNullOrWhiteSpace(selectedLayout)) return;
        Layout = selectedLayout;
        layoutChanged(selectedLayout);
    }

    // Performs the property changed notification action for this screen or workflow.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

// Describes one dashboard KPI tile rendered by the dashboard AXAML template.
public sealed record DashboardTileModel(string Label, string Value, string Icon, IBrush Accent, IBrush IconBackground);

// Describes one recent invoice row and its action commands for the dashboard AXAML template.
public sealed class DashboardInvoiceModel
{
    public int Number { get; init; }
    public string InvoiceTitle { get; init; } = "";
    public string CustomerLine { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string DateLine { get; init; } = "";
    public string RawDate { get; init; } = "";
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
