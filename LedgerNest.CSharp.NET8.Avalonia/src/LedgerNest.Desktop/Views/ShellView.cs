using Avalonia;
using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private MainWindowViewModel? shellModel;
    private System.ComponentModel.PropertyChangedEventHandler? shellChanged;

    // Performs the initialize shell action for this screen or workflow.
    private void InitializeShell(MainWindowViewModel vm)
    {
        if (shellModel != null && shellChanged != null) shellModel.PropertyChanged -= shellChanged;
        page.Content = null;
        sidebar.Content = null;
        overlay.Children.Clear();
        overlay.IsVisible = false;
        shellModel = vm;
        ShowStatusToast(vm.Status);
        shellChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Title)) { ShowPage(); if (vm.CanAccessWorkspace) BuildSidebar(); }
            if (e.PropertyName is nameof(vm.SidebarExpanded) or nameof(vm.CurrentUsername))
                if (vm.CanAccessWorkspace) BuildSidebar();
            if (e.PropertyName == nameof(vm.CanAccessWorkspace)) RefreshWorkspaceAccess();
            if (e.PropertyName == nameof(vm.Status)) ShowStatusToast(vm.Status);
        };
        vm.PropertyChanged += shellChanged;
        RefreshWorkspaceAccess();
    }
    // Performs the build sidebar action for this screen or workflow.
    private void BuildSidebar()
    {
        sidebar.Content = new SidebarView(Model, route => Model.NavigateCommand.Execute(route), () => Model.ToggleSidebarCommand.Execute(null), () => { Model.SignOut(); ShowLogin(); });
    }

    // Performs direct invoice navigation from a customer row with the customer search applied.
    internal void OpenInvoicesForCustomer(string customerName)
    {
        Model.QueueInvoiceCustomerFilter(customerName);
        if (Model.Title == "Invoices") ShowPage(); else Model.NavigateCommand.Execute("Invoices");
    }

    // Performs direct customer report navigation from a customer row with the statement filter applied.
    internal void OpenCustomerReport(string customerName)
    {
        Model.QueueCustomerReportFilter(customerName);
        if (Model.Title == "Reports") ShowPage(); else Model.NavigateCommand.Execute("Reports");
    }

    // Performs the show page action for this screen or workflow.
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
            "Reports" => Reports(), 
            "Settings" => SettingsView(), 
            _ => Dashboard()
        };
    }
}
