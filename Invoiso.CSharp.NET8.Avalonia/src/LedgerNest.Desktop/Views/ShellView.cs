using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private void InitializeShell(MainWindowViewModel vm)
    {
        Root.Children.Clear();
        var body = Ui.Columns("Auto,*", sidebar, page);
        var statusBar = new Border { Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(16, 8), Child = Ui.Columns("*,Auto", status, Ui.Button("×", () => vm.Status = "")), IsVisible = false };
        Root.Children.Add(Ui.Rows("*,Auto", body, statusBar)); Root.Children.Add(overlay);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Title)) { ShowPage(); BuildSidebar(); }
            if (e.PropertyName == nameof(vm.SidebarExpanded)) BuildSidebar();
            if (e.PropertyName == nameof(vm.Status)) { status.Text = vm.Status; statusBar.IsVisible = vm.Status.Length > 0; }
        };
        BuildSidebar(); ShowPage();
    }
    private void BuildSidebar()
    {
        var expanded = Model.SidebarExpanded;
        Control logo = expanded ? Ui.Columns("*,Auto", Ui.Logo(), Ui.Button("‹", () => Model.ToggleSidebarCommand.Execute(null))) : Ui.Stack(2, Ui.Logo(true), Ui.Button("›", () => Model.ToggleSidebarCommand.Execute(null)));
        var nav = Ui.Stack(0);
        string[] icons = ["dashboard", "receipt", "receipt_long", "request_quote", "point_of_sale", "people", "inventory_2", "bar_chart", "settings"];
        for (var i = 0; i < MainWindowViewModel.Routes.Length; i++)
        {
            var route = MainWindowViewModel.Routes[i];
            var button = Ui.Button(route, () => Model.NavigateCommand.Execute(route));
            var selected = Model.Title == route;
            var marker = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), Background = Ui.Primary, IsVisible = selected && expanded };
            button.Content = expanded ? Ui.Columns("18,12,*,Auto", Ui.Icon(icons[i], 18, selected ? Ui.Primary : Ui.Muted), new Border(), Ui.Text(route, 13.5, selected, selected ? Ui.Primary : Ui.Muted), marker) : Ui.Icon(icons[i], 20, selected ? Ui.Primary : Ui.Muted);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Classes.Clear(); button.Classes.Add("nav"); if (Model.Title == route) button.Classes.Add("selected");
            ToolTip.SetTip(button, route); nav.Children.Add(button);
        }
        var avatar = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Brush.Parse("#DDE3ED"), Child = Ui.Text("A", 12, true, Ui.Primary) }; ((TextBlock)avatar.Child!).HorizontalAlignment = HorizontalAlignment.Center;
        var logout = Ui.Button("⇥", ShowLogin); logout.Classes.Add("text"); logout.Padding = new Thickness(4); logout.MinHeight = 30;
        var footer = expanded ? Ui.Stack(8, Ui.Columns("30,10,*,Auto", avatar, new Border(), Ui.Stack(2, Ui.Text("admin", 13), Ui.Text("Admin", 11, color: Ui.Muted)), logout), new TextBlock { Text = "v4.4.0", FontSize = 12, Foreground = Ui.Outline, HorizontalAlignment = HorizontalAlignment.Center }) : Ui.Stack(8, avatar, logout);
        sidebar.Content = new Border { Width = expanded ? 210 : 64, Background = Brush.Parse("#FAFAFA"), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 1, 0), Child = Ui.Rows("76,*,Auto", new Border { Padding = new Thickness(expanded ? 12 : 0, 0), Child = logo }, new Border { Padding = new Thickness(0, 8, 0, 0), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1, 0, 0), Child = Ui.Scroll(nav, 0) }, new Border { BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Outline, Padding = new Thickness(14, 12), Child = footer }) };
    }
    private void ShowPage()
    {
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
