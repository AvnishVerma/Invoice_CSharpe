using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementPaginationView : UserControl, INotifyPropertyChanged
{
    private string summary = "";
    private string pageLabel = "";
    private bool canGoPrevious;
    private bool canGoNext;
    private IRelayCommand previousCommand = new RelayCommand(() => { });
    private IRelayCommand nextCommand = new RelayCommand(() => { });

    private event PropertyChangedEventHandler? propertyChanged;
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => propertyChanged += value;
        remove => propertyChanged -= value;
    }
    public string Summary { get => summary; set => Set(ref summary, value); }
    public string PageLabel { get => pageLabel; set => Set(ref pageLabel, value); }
    public bool CanGoPrevious { get => canGoPrevious; set => Set(ref canGoPrevious, value); }
    public bool CanGoNext { get => canGoNext; set => Set(ref canGoNext, value); }
    public IRelayCommand PreviousCommand { get => previousCommand; private set => Set(ref previousCommand, value); }
    public IRelayCommand NextCommand { get => nextCommand; private set => Set(ref nextCommand, value); }

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

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
