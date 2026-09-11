using Avalonia;
using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private MainWindowViewModel? shellModel;
    private System.ComponentModel.PropertyChangedEventHandler? shellChanged;

    private void InitializeShell(MainWindowViewModel vm)
    {
        if (shellModel != null && shellChanged != null) shellModel.PropertyChanged -= shellChanged;
        page.Content = null;
        sidebar.Content = null;
        overlay.Children.Clear(); overlay.IsVisible = false;
        shellModel = vm;
        status.Text = vm.Status;
        statusBar.Background = Ui.Palette("#E8F5E9", "#183A2D");
        statusBar.IsVisible = vm.Status.Length > 0;
        shellChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Title)) { ShowPage(); if (vm.CanAccessWorkspace) BuildSidebar(); }
            if (e.PropertyName is nameof(vm.SidebarExpanded) or nameof(vm.CurrentUsername))
                if (vm.CanAccessWorkspace) BuildSidebar();
            if (e.PropertyName == nameof(vm.CanAccessWorkspace)) RefreshWorkspaceAccess();
            if (e.PropertyName == nameof(vm.Status)) { status.Text = vm.Status; statusBar.IsVisible = vm.Status.Length > 0; }
        };
        vm.PropertyChanged += shellChanged;
        RefreshWorkspaceAccess();
    }
    private void BuildSidebar()
    {
        sidebar.Content = new SidebarView(Model, route => Model.NavigateCommand.Execute(route), () => Model.ToggleSidebarCommand.Execute(null), () => { Model.SignOut(); ShowLogin(); });
    }
    private void ShowPage()
    {
        invoiceCompletionVisible = false;
        if (!Model.ValidateSession() || !Model.CanAccessWorkspace) { ShowAccessScreen(); return; }
        CloseOverlay();
        page.Content = Model.Title switch
        {
            "Dashboard" => Dashboard(), "New Invoice" => InvoiceEditor(),
            "Customers" => new ManagementView(Model, "Customer", this),
            "Products" => new ManagementView(Model, "Product", this),
            "Invoices" => new ManagementView(Model, "Invoice", this),
            "Quotations" => new ManagementView(Model, "Quotation", this),
            "Receipts" => new ManagementView(Model, "Receipt", this),
            "Reports" => Reports(), "Settings" => SettingsView(), _ => Dashboard()
        };
    }
}
