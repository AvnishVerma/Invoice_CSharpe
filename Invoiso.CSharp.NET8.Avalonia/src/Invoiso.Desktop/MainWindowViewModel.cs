using CommunityToolkit.Mvvm.ComponentModel;

namespace Invoiso.Desktop;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Dashboard";
}
