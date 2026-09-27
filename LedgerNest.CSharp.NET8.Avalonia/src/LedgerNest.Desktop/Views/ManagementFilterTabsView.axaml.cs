using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementFilterTabsView : UserControl
{
    public ObservableCollection<ManagementFilterTab> Items { get; } = [];

    public ManagementFilterTabsView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void SetItems(IEnumerable<(string Name, int Count)> values, string selected, Action<string> select)
    {
        Items.Clear();
        foreach (var value in values)
            Items.Add(new ManagementFilterTab(value.Name, value.Count, value.Name == selected, select));
    }
}

public sealed class ManagementFilterTab
{
    private readonly Action<string> select;
    public string Name { get; }
    public int Count { get; }
    public bool IsSelected { get; }
    public string Label => $"{Name} ({Count})";
    public IRelayCommand<ManagementFilterTab> SelectCommand { get; }

    public ManagementFilterTab(string name, int count, bool isSelected, Action<string> select)
    {
        Name = name;
        Count = count;
        IsSelected = isSelected;
        this.select = select;
        SelectCommand = new RelayCommand<ManagementFilterTab>(_ => this.select(Name));
    }
}
