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
    private bool invoiceCompletionVisible;
    public MainWindow()
    {
        InitializeComponent();
        Ui.UpdateTheme(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        Background = Ui.Canvas;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualThemeVariantProperty)
                Ui.UpdateTheme(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        };
        Activated += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.ValidateSession(); };
        Closed += (_, _) =>
        {
            if (shellModel != null && shellChanged != null) shellModel.PropertyChanged -= shellChanged;
            page.Content = null;
        };
        Title = Branding.Name;
        DataContextChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) InitializeShell(vm); };
        KeyDown += (_, e) =>
        {
            if (DataContext is not MainWindowViewModel currentModel) return;
            if (((e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key == Key.Escape) && !currentModel.ValidateSession()) || !currentModel.CanAccessWorkspace)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key == Key.Escape) e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && overlay.IsVisible) { CloseOverlay(); e.Handled = true; }
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (e.Key == Key.Q) { Model.StartDocument("Invoice"); ShowPage(); e.Handled = true; }
            if (e.Key == Key.S && Model.Title == "New Invoice" && !invoiceCompletionVisible && !overlay.IsVisible) { if (Model.SaveInvoice()) ShowInvoiceSuccess(); e.Handled = true; }
            if (e.Key == Key.M && Model.Title == "New Invoice") { ShowCustomItem(); e.Handled = true; }
        };
    }

    private void OnDismissStatus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Status = "";
    }
}
