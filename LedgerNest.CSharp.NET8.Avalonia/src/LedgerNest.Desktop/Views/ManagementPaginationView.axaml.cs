using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementPaginationView : UserControl
{
    public string Summary { get; set; } = "";
    public string PageLabel { get; set; } = "";
    public bool CanGoPrevious { get; set; }
    public bool CanGoNext { get; set; }
    public IRelayCommand PreviousCommand { get; private set; } = new RelayCommand(() => { });
    public IRelayCommand NextCommand { get; private set; } = new RelayCommand(() => { });

    public ManagementPaginationView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ManagementPaginationView(Control? pageSizeControl, string summary, int page, int pages, Action previous, Action next)
        : this()
    {
        PageSizeHost.Content = pageSizeControl;
        Summary = summary;
        PageLabel = $"Page {page + 1} of {pages}";
        CanGoPrevious = page > 0;
        CanGoNext = page + 1 < pages;
        PreviousCommand = new RelayCommand(previous);
        NextCommand = new RelayCommand(next);
    }
}
