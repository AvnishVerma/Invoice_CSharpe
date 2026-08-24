using CommunityToolkit.Mvvm.ComponentModel;

namespace Invoiso.Application;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Invoiso";

    [ObservableProperty]
    private string status = "C# / .NET 8 / Avalonia / SQLite";
}
