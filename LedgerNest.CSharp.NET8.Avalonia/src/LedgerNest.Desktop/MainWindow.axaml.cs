using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow : Window
{
    private readonly IHtmlPrintService htmlPrintService;
    private readonly bool ownsHtmlPrintService;
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;
    private bool invoiceCompletionVisible;
    public MainWindow() : this(new PlaywrightHtmlPrintService(), true)
    {
    }

    // Creates the main window with the application-scoped HTML printing service.
    public MainWindow(IHtmlPrintService htmlPrintService) : this(htmlPrintService, true)
    {
    }

    // Initializes the window and records whether it owns the printing service lifetime.
    private MainWindow(IHtmlPrintService htmlPrintService, bool ownsHtmlPrintService)
    {
        this.htmlPrintService = htmlPrintService;
        this.ownsHtmlPrintService = ownsHtmlPrintService;
        InitializeComponent();
        Ui.UpdateTheme(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        Background = Ui.Canvas;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualThemeVariantProperty)
                Ui.UpdateTheme(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        };
        Activated += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.ValidateSession(); };
        Closed += async (_, _) =>
        {
            if (shellModel != null && shellChanged != null) shellModel.PropertyChanged -= shellChanged;
            page.Content = null;
            if (this.ownsHtmlPrintService) await this.htmlPrintService.DisposeAsync();
        };
        Title = Branding.Name;
        DataContextChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) InitializeShell(vm); };
        RegisterShortcut(Key.Q);
        RegisterShortcut(Key.S);
        RegisterShortcut(Key.F);
        RegisterShortcut(Key.M);
        RegisterShortcut(Key.O);
        RegisterShortcut(Key.P);
        RegisterShortcut(Key.Escape, KeyModifiers.None);
    }

    // Performs shortcut key binding registration for commands shown in the UI.
    private void RegisterShortcut(Key key, KeyModifiers modifiers = KeyModifiers.Control)
    {
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(key, modifiers),
            Command = new RelayCommand(() => ExecuteShortcut(key, modifiers))
        });
    }

    // Performs the requested shortcut action when the current page and session allow it.
    private bool ExecuteShortcut(Key key, KeyModifiers modifiers)
    {
        if (DataContext is not MainWindowViewModel currentModel) return false;
        var isControlShortcut = modifiers.HasFlag(KeyModifiers.Control);
        if (((isControlShortcut || key == Key.Escape) && !currentModel.ValidateSession()) || !currentModel.CanAccessWorkspace)
            return isControlShortcut || key == Key.Escape;

        if (key == Key.Escape && overlay.IsVisible)
        {
            CloseOverlay();
            return true;
        }

        if (!isControlShortcut || overlay.IsVisible) return false;

        switch (key)
        {
            case Key.Q:
                Model.StartDocument("Invoice");
                ShowPage();
                return true;
            case Key.S when Model.Title == "New Invoice" && !invoiceCompletionVisible:
                if (Model.SaveInvoice()) ShowInvoiceSuccess();
                return true;
            case Key.F when Model.Title == "New Invoice":
                FocusInvoiceProductSearch();
                return true;
            case Key.M when Model.Title == "New Invoice":
                ShowCustomItem();
                return true;
            case Key.O when Model.Title == "New Invoice" && Model.LastSavedDocument != null:
                ShowPdfPreview(Model.LastSavedDocument);
                return true;
            case Key.P when Model.Title == "New Invoice" && Model.LastSavedDocument != null:
                _ = PrintDocumentAsync(Model.LastSavedDocument);
                return true;
            default:
                return false;
        }
    }

    // Performs focus movement to the product search box used by the invoice editor.
    private void FocusInvoiceProductSearch()
    {
        var search = page.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(t => t.PlaceholderText == "Search & add a product or service (Ctrl+F)");
        search?.Focus();
        search?.SelectAll();
    }

    // Performs the on dismiss status action for this screen or workflow.
    private void OnDismissStatus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Status = "";
    }
}
