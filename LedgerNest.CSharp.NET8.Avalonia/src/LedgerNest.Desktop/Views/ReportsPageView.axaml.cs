using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ReportsPageView : UserControl
{
    // Performs the reports page view initialization action for this screen or workflow.
    public ReportsPageView()
    {
        InitializeComponent();
    }

    // Performs the reports page view data context assignment action for this screen or workflow.
    public ReportsPageView(ReportsPageModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides report navigation state and content loading for the AXAML reports shell.
public sealed class ReportsPageModel : INotifyPropertyChanged
{
    private readonly Func<string, Control> contentFactory;
    private readonly Action<string> selectedChanged;
    private string selectedReport;
    private Control currentContent;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<string> ReportTabs { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectReportCommand { get; }

    // Gets or changes the selected report tab and refreshes the hosted report content.
    public string SelectedReport
    {
        get => selectedReport;
        set
        {
            if (selectedReport == value || string.IsNullOrWhiteSpace(value)) return;
            selectedReport = value;
            selectedChanged(value);
            CurrentContent = contentFactory(value);
            OnPropertyChanged();
        }
    }

    // Gets the currently rendered report body hosted by the AXAML content area.
    public Control CurrentContent
    {
        get => currentContent;
        private set
        {
            currentContent = value;
            OnPropertyChanged();
        }
    }

    // Performs the reports page model initialization action for this screen or workflow.
    public ReportsPageModel(IEnumerable<string> reportTabs, string selectedReport, Func<string, Control> contentFactory, Action<string> selectedChanged)
    {
        this.contentFactory = contentFactory;
        this.selectedChanged = selectedChanged;
        ReportTabs = new ObservableCollection<string>(reportTabs);
        this.selectedReport = selectedReport;
        currentContent = contentFactory(selectedReport);
        RefreshCommand = new RelayCommand(() => CurrentContent = contentFactory(SelectedReport));
        SelectReportCommand = new RelayCommand<string>(report =>
        {
            if (!string.IsNullOrWhiteSpace(report)) SelectedReport = report;
        });
    }

    // Performs the property changed notification action for this screen or workflow.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
