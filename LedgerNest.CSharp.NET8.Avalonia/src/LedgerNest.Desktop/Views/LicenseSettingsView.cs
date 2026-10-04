using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private Control LicenseWorkspaceContent(Control body) => new LicenseWorkspaceView(Model, body, () =>
    {
        settingsTab = "License";
        if (Model.Title == "Settings") ShowPage();
        else Model.NavigateCommand.Execute("Settings");
    });

    private Control LicenseSettingsView() => new LicenseSettingsPageView { DataContext = new LicenseSettingsViewModel(Model) };
}
