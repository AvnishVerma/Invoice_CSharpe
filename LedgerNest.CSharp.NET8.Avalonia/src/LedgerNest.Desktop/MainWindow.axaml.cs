using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow : Window
{
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;
    private readonly ContentControl page = new();
    private readonly ContentControl sidebar = new();
    private readonly Grid overlay = new() { IsVisible = false };
    private readonly TextBlock status = Ui.Text("", 13);
    public MainWindow()
    {
        InitializeComponent();
        Title = Branding.Name;
        DataContextChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) InitializeShell(vm); };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && overlay.IsVisible) { CloseOverlay(); e.Handled = true; }
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (e.Key == Key.Q) { Model.NavigateCommand.Execute("New Invoice"); e.Handled = true; }
            if (e.Key == Key.S && Model.Title == "New Invoice") { if (Model.SaveInvoice()) ShowInvoiceSuccess(); e.Handled = true; }
            if (e.Key == Key.M && Model.Title == "New Invoice") { ShowCustomItem(); e.Handled = true; }
        };
    }
}
