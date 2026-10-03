using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private Control CompanySettingsView() => new CompanySettingsView { DataContext = new CompanySettingsViewModel(Model) };
    private Control PdfSettingsView() => new PdfSettingsView { DataContext = new PdfSettingsViewModel(Model, printService) };
}
