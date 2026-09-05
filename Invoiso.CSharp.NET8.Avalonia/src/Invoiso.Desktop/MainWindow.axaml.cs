using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Invoiso.Desktop.Views;

namespace Invoiso.Desktop;

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
    internal void CloseOverlay() { overlay.Children.Clear(); overlay.IsVisible = false; }
    internal void ShowOverlay(string title, Control content, Control? footer = null, bool side = false, double width = 560, Control? headerAccessory = null)
    {
        overlay.Children.Clear(); overlay.IsVisible = true;
        overlay.Margin = new Thickness(side ? (Model.SidebarExpanded ? 210 : 64) : 0, 0, 0, 0);
        var scrim = new Border { Background = Brush.Parse("#4D000000") }; scrim.PointerPressed += (_, _) => CloseOverlay(); overlay.Children.Add(scrim);
        var panel = Ui.Card(Ui.Rows("Auto,*,Auto", new Border { Padding = new Thickness(18, 16), Child = Ui.Columns("*,Auto,Auto", Ui.Text(title, 16, true), headerAccessory ?? new Border(), Ui.Button("×", CloseOverlay)) }, Ui.Scroll(content, 18), new Border { Padding = new Thickness(18), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1, 0, 0), Child = footer ?? Ui.Button("Close", CloseOverlay) }), 0);
        var availableWidth = Bounds.Width - overlay.Margin.Left;
        panel.Width = side ? (availableWidth < 750 ? availableWidth - 32 : Math.Clamp(availableWidth * .42, 520, 680)) : width; panel.MaxWidth = Math.Max(280, availableWidth - 32); panel.MaxHeight = Math.Max(320, Bounds.Height - 32); panel.Margin = new Thickness(16);
        panel.HorizontalAlignment = side ? HorizontalAlignment.Right : HorizontalAlignment.Center; panel.VerticalAlignment = side ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        overlay.Children.Add(panel);
    }
    internal void Confirm(string title, string message, Action action)
    { ShowOverlay(title, Ui.Text(message), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Confirm", () => { action(); CloseOverlay(); }, true)), width: 460); }
    internal void EditRecord(string kind, Action refresh, UiRecord? record = null)
    {
        var fields = kind == "Customer" ? FormCatalog.Customer() : kind == "Product" ? FormCatalog.Product() : FormCatalog.User();
        if (record != null && kind == "User") fields = fields.Where(f => f.Kind != "password").ToArray();
        if (record != null) foreach (var f in fields) { f.Value = record[f.Label]; f.IsChecked = bool.TryParse(f.Value, out var v) && v; }
        var another = new CheckBox { Content = "Add another after saving", IsVisible = record == null };
        var cancel = Ui.Button("Cancel", CloseOverlay); cancel.CornerRadius = new CornerRadius(24); cancel.HorizontalAlignment = HorizontalAlignment.Stretch;
        var save = Ui.Button($"Save {kind}", () =>
        {
            if (!Model.SaveRecord(kind, fields, record)) return;
            refresh(); if (another.IsChecked == true) EditRecord(kind, refresh); else CloseOverlay();
        }, true); save.Classes.Add("material"); save.CornerRadius = new CornerRadius(24); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        Control form = Ui.Fields(fields);
        if (kind == "Product")
        {
            form = Ui.Stack(20, Ui.Text("GENERAL", 11, true, Ui.Muted), Ui.Fields(fields.Skip(1).Take(4)), Ui.Text("PRICING", 11, true, Ui.Muted), Ui.Fields(fields.Skip(5).Take(2), 2), Ui.Field(fields[7]), Ui.Fields(fields.Skip(8).Take(2), 2), Ui.Text("STOCK & UNIT", 11, true, Ui.Muted), Ui.Fields(fields.Skip(10).Take(4), 2), new Expander { Header = "Advanced Information", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Fields(fields.Skip(14), 2) });
        }
        ShowOverlay(record == null ? (kind == "Product" ? "Add New Product" : $"New {kind}") : $"Edit {kind}", form, Ui.Stack(16, another, Ui.Columns("*,12,2*", cancel, new Border(), save)), true, kind == "Product" ? 550 : 520, kind == "Product" ? Ui.Segments(fields[0]) : null);
    }

    internal void ShowPayment(UiRecord invoice)
    {
        var fields = FormCatalog.Payment();
        ShowOverlay("Apply Payment", Ui.Stack(18, Ui.Text($"Invoice: {invoice.Name} · {invoice["Customer"]}"), Ui.Stats(("Invoice Total", invoice["Total"], "", "#002E78"), ("Amount Paid", "0.00", "", "#2E7D32"), ("Outstanding", invoice["Total"], "", "#C62828")), Ui.Text("Payment History", 16, true), Ui.Empty("No payments yet", "", ""), Ui.Text("New Payment", 16, true), Ui.Fields(fields, 2)), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Save Payment")), width: 760);
    }
    private void ShowCustomItem()
    {
        var fields = FormCatalog.CustomItem();
        ShowOverlay("Add Custom Item", Ui.Fields(fields, 2), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Add Item", () =>
        {
            if (!fields.Select(f => f.Validate()).ToArray().All(v => v) || fields[2].Number <= 0) { fields[2].Error = "Quantity must be greater than zero."; return; }
            Model.Lines.Add(new InvoiceLineViewModel { Name = fields[0].Value, Quantity = fields[2].Number, Price = fields[3].Number, TaxRate = fields[4].Number, Discount = fields[5].Number }); CloseOverlay();
        }, true)), width: 640);
    }
}
