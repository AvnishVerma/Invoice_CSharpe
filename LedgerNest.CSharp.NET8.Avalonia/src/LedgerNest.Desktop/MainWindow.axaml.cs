using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Views;
using LedgerNest.Desktop.Printing;
using LedgerNest.Desktop.Notifications;
using LedgerNest.Desktop.Updates;

namespace LedgerNest.Desktop;

public partial class MainWindow : Window
{
    private readonly IPrintService printService;
    private readonly IPdfGenerator pdfGenerator;
    private readonly IToastService toastService;
    private readonly IAppUpdateService updateService;
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;
    private bool invoiceCompletionVisible;
    private readonly Avalonia.Threading.DispatcherTimer licenseTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    public MainWindow() : this(new PrintServiceFactory().Create(), new TemporaryPdfGenerator(), new AvaloniaToastService(), new HttpAppUpdateService())
    {
    }

    // Creates the main window with application-scoped PDF generation and native printing services.
    public MainWindow(IPrintService printService, IPdfGenerator pdfGenerator, IToastService toastService)
        : this(printService, pdfGenerator, toastService, new HttpAppUpdateService())
    {
    }

    internal MainWindow(IPrintService printService, IPdfGenerator pdfGenerator, IToastService toastService, IAppUpdateService updateService)
    {
        this.printService = printService;
        this.pdfGenerator = pdfGenerator;
        this.toastService = toastService;
        this.updateService = updateService;
        InitializeComponent();
        InitializeToasts();
        Ui.UpdateTheme(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        Background = Ui.Canvas;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualThemeVariantProperty)
                Ui.UpdateTheme(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        };
        Activated += (_, _) => { if (DataContext is MainWindowViewModel vm) { vm.ValidateSession(); vm.RefreshLicense(); } };
        licenseTimer.Tick += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.RefreshLicense(); };
        Opened += async (_, _) =>
        {
            licenseTimer.Start();
            await CheckForUpdatesAsync(silent: true);
        };
        Closed += (_, _) =>
        {
            if (shellModel != null && shellChanged != null) shellModel.PropertyChanged -= shellChanged;
            toastService.Requested -= OnToastRequested;
            toastTimer?.Stop();
            licenseTimer.Stop();
            page.Content = null;
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
                _ = ShowPdfPreviewAsync(Model.LastSavedDocument);
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

    // Checks the configured publisher endpoint and announces a newly available release.
    private async Task CheckForUpdatesAsync(bool silent = false)
    {
        var result = await updateService.CheckAsync(Model.UpdateManifestUrl);
        Model.ApplyUpdateCheck(result);
        if (result.IsUpdateAvailable)
            toastService.Show("Update available", result.Message, ToastType.Info, isPersistent: true);
        else if (!silent)
            toastService.Show(result.State == UpdateCheckState.Failed ? "Update check failed" : "Updates", result.Message,
                result.State == UpdateCheckState.Failed ? ToastType.Warning : ToastType.Success);
        if (Model.Title == "Settings") ShowPage();
    }

}
