using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Updates;

namespace LedgerNest.Desktop.Views;

public sealed partial class SidebarView : UserControl
{
    public SidebarView() => InitializeComponent();
    public SidebarView(MainWindowViewModel model, Action<string> navigate, Action toggleSidebar, Action logout) : this()
        => DataContext = new SidebarViewModel(model, navigate, toggleSidebar, logout);
}

public sealed class SidebarViewModel
{
    public bool Expanded { get; }
    public double Width => Expanded ? 210 : 64;
    public string Username { get; }
    public string Role { get; }
    public string Initial { get; }
    public string Version => $"v{AppVersion.Display}";
    public ICommand ToggleCommand { get; }
    public ICommand LogoutCommand { get; }
    public ObservableCollection<SidebarItemViewModel> Items { get; } = [];

    public SidebarViewModel(MainWindowViewModel model, Action<string> navigate, Action toggleSidebar, Action logout)
    {
        Expanded = model.SidebarExpanded;
        Username = model.CurrentUsername ?? "Not signed in";
        Role = model.CurrentRole;
        Initial = string.IsNullOrWhiteSpace(model.CurrentUsername) ? "?" : model.CurrentUsername[..1].ToUpperInvariant();
        ToggleCommand = new RelayCommand(toggleSidebar);
        LogoutCommand = new RelayCommand(logout);
        var icons = MainWindowViewModel.Routes.Zip(new[] { "dashboard", "receipt", "receipt_long", "request_quote", "point_of_sale", "people", "inventory_2", "straighten", "payments", "warehouse", "bar_chart", "settings" }).ToDictionary(item => item.First, item => item.Second);
        foreach (var route in model.VisibleRoutes)
            Items.Add(new SidebarItemViewModel(route, icons[route], Expanded, model.Title == route, new RelayCommand(() => navigate(route))));
    }
}

public sealed record SidebarItemViewModel(string Route, string Icon, bool Expanded, bool IsSelected, ICommand NavigateCommand)
{
    public FontWeight Weight => IsSelected ? FontWeight.SemiBold : FontWeight.Normal;
}
