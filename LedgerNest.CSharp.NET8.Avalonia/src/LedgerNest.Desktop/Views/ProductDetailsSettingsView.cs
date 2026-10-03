using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    internal void OpenProductDetailsSettings()
    {
        settingsTab = "Product Details";
        if (Model.Title == "Settings") ShowPage(); else Model.NavigateCommand.Execute("Settings");
    }

    private Control ProductDetailsSettingsView() => new ProductDetailsSettingsView { DataContext = new ProductDetailsSettingsViewModel(Model) };
}
