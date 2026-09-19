using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class SettingsPageView : UserControl
{
    // Performs the settings page view initialization action for this screen or workflow.
    public SettingsPageView()
    {
        InitializeComponent();
    }

    // Performs the settings page view data context assignment action for this screen or workflow.
    public SettingsPageView(SettingsPageModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Describes one settings rail item rendered by the AXAML settings shell.
public sealed partial class SettingsTabModel(string label, string icon) : ObservableObject
{
    public string Label { get; } = label;
    public string Icon { get; } = icon;
    [ObservableProperty] private bool isSelected;
}

// Provides settings navigation state and content loading for the AXAML settings shell.
public sealed class SettingsPageModel : INotifyPropertyChanged
{
    private readonly Func<string, Control> contentFactory;
    private readonly Action<string> selectedChanged;
    private string selectedTab;
    private Control currentContent;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<SettingsTabModel> Tabs { get; }
    public ICommand SelectTabCommand { get; }

    // Gets or changes the selected settings tab and refreshes the hosted settings content.
    public string SelectedTab
    {
        get => selectedTab;
        set
        {
            if (selectedTab == value || string.IsNullOrWhiteSpace(value)) return;
            selectedTab = value;
            foreach (var tab in Tabs) tab.IsSelected = tab.Label == value;
            selectedChanged(value);
            CurrentContent = contentFactory(value);
            OnPropertyChanged();
        }
    }

    // Gets the currently rendered settings page hosted by the AXAML content area.
    public Control CurrentContent
    {
        get => currentContent;
        private set
        {
            currentContent = value;
            OnPropertyChanged();
        }
    }

    // Performs the settings page model initialization action for this screen or workflow.
    public SettingsPageModel(IEnumerable<SettingsTabModel> tabs, string selectedTab, Func<string, Control> contentFactory, Action<string> selectedChanged)
    {
        this.contentFactory = contentFactory;
        this.selectedChanged = selectedChanged;
        Tabs = new ObservableCollection<SettingsTabModel>(tabs);
        this.selectedTab = selectedTab;
        foreach (var tab in Tabs) tab.IsSelected = tab.Label == selectedTab;
        currentContent = contentFactory(selectedTab);
        SelectTabCommand = new RelayCommand<string>(tab =>
        {
            if (!string.IsNullOrWhiteSpace(tab)) SelectedTab = tab;
        });
    }

    // Performs the property changed notification action for this screen or workflow.
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
