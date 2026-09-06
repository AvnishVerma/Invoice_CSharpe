using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Application;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "LedgerNest";

    [ObservableProperty]
    private string status = "C# / .NET 8 / Avalonia / SQLite";
}
