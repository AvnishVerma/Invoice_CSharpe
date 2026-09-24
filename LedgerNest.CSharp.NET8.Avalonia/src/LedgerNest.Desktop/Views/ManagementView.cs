using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Text;

namespace LedgerNest.Desktop.Views;

internal sealed partial class ManagementView : UserControl
{
    private readonly MainWindowViewModel model;
    private readonly MainWindow window;
    private readonly string kind;
    private readonly ContentControl results = new();
    private readonly ContentControl stats = new();
    private readonly TextBox search = new() { MinWidth = 180, Height = 38, MinHeight = 38, Padding = new Thickness(12, 0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl tabs = new();
    private string filter = "All";
    private string sort = "Name A–Z";
    private int page;
    private int pageSize = 10;
    private bool trash;
    private HashSet<Guid> deleted => model.DeletedRecords;
    private readonly HashSet<Guid> selected = [];
    private bool Documents => kind is "Invoice" or "Quotation" or "Receipt";
    private IEnumerable<UiRecord> Records => (kind == "Customer" ? model.Customers : kind == "Product" ? model.Products : kind == "User" ? model.Users : model.Invoices.Where(r => r["Type"] == kind)).Where(r => deleted.Contains(r.Id) == trash);
    public ManagementView(MainWindowViewModel model, string kind, MainWindow window)
    {
        InitializeComponent();
        this.model = model; this.kind = kind; this.window = window;
        search.PlaceholderText = Documents ? "Search by Invoice ID or Customer Name…" : kind == "Customer" ? "Search customers by name, phone, email, GST…" : kind == "Product" ? "Search products by name, alias, HSN/SAC, SKU…" : "Search users…";
        search.TextChanged += (_, _) => { page = 0; Refresh(); };
        var add = Ui.Button($"＋ New {kind}", () => { if (Documents) model.StartDocument(kind); else window.EditRecord(kind, Refresh); }, true); add.Classes.Add("material");
        var more = MoreMenu();
        var trashButton = Ui.Button("Trash", () => { trash = !trash; Refresh(); });
        var headerActions = kind == "User"
            ? new Control[] { Ui.Button("↻", Refresh), add }
            : Documents
            ? new Control[] { Ui.Button("↑ Import", Import), Ui.Button("↓ Export", Export), more, trashButton, Ui.Button("↻", Refresh) }
            : [Ui.Button("↑ Import", Import), Ui.Button("↓ Export", Export), more, Ui.Button("↻", Refresh), add];
        var header = Ui.AppBar($"{kind} Management", headerActions);
        var filterButton = MenuButton("Filter ▾", FilterOptions(), option => { filter = option; page = 0; Refresh(); });
        var sortButton = MenuButton("Sort: Name A–Z ▾", ["Name A–Z", "Name Z–A", "Newest", "Oldest"], option => { sort = option; Refresh(); });
        if (kind == "Product")
        {
            var banner = Ui.Card(Ui.Columns("*,Auto", Ui.Stack(4, Ui.Text("New: Customize product fields", 13, true, Ui.Primary), Ui.Text("Choose which fields show for a simpler catalog. Settings > Customize Product Details.", 12, color: Ui.Primary)), Ui.Button("Configure", () => window.OpenProductDetailsSettings())), 16);
            banner.Background = Brush.Parse("#EFF6FF");
            ProductBannerHost.Content = banner;
        }
        if (Documents)
        {
            RecordRoot.IsVisible = false;
            DocumentRoot.IsVisible = true;
            DocumentAppBarHost.Content = Ui.AppBar($"{kind} Management", TopIconAction("download", "Export PDF", async () => await ExportDocumentsPdf()), TopIconAction("download", "Export", Export), TopIconAction("delete", "Trash", () => { trash = !trash; Refresh(); }), TopIconAction("refresh", "Refresh", Refresh));
            DocumentAddHost.Content = add;
            DocumentSearchHost.Content = search;
            DocumentToolbarHost.Content = Ui.Wrap(CustomerMenu(), filterButton, sortButton);
            DocumentResultsHost.Content = results;
        }
        else
        {
            RecordRoot.IsVisible = true;
            DocumentRoot.IsVisible = false;
            HeaderHost.Content = header;
            StatsHost.Content = stats;
            SearchHost.Content = search;
            ToolbarButtonsHost.Content = kind == "User"
                ? MenuButton("Role: All ▾", FilterOptions(), option => { filter = option; page = 0; Refresh(); })
                : Ui.Wrap(filterButton, sortButton, Ui.Button("Columns ▾", Columns), Ui.Button("◉", () => stats.IsVisible = !stats.IsVisible));
            TabsHost.Content = tabs;
            TabsHost.IsVisible = kind != "User";
            RecordResultsHost.Content = results;
        }
        if (Documents && kind == "Invoice")
        {
            var pendingCustomer = model.ConsumePendingInvoiceCustomerFilter();
            if (!string.IsNullOrWhiteSpace(pendingCustomer)) search.Text = pendingCustomer;
        }
        Refresh();
    }
    // Performs the subtitle action for this screen or workflow.
    private string Subtitle() => kind == "Customer" ? "Manage your customers and contact details" : kind == "Product" ? "Manage your products and services" : Documents ? $"Manage {kind.ToLowerInvariant()}s and payment status" : "Manage users and access permissions";
    // Performs the filter options action for this screen or workflow.
    private string[] FilterOptions() => Documents ? ["All", "Paid", "Partial", "Unpaid", "Overdue"] : kind == "Customer" ? ["All", "Businesses", "Individuals", "GST Registered", "Without GST", "With Outstanding"] : kind == "Product" ? ["All", "Products", "Services", "Low Stock", "Out of Stock", "Expired"] : ["All", "Admin", "User"];
    // Performs the menu button action for this screen or workflow.
    private Button MenuButton(string title, IEnumerable<string> options, Action<string> select)
    {
        var button = Ui.Button(title, () => { });
        var menu = new MenuFlyout();
        foreach (var option in options)
        {
            var item = new MenuItem { Header = option };
            item.Click += (_, _) => { menu.Hide(); select(option); };
            menu.Items.Add(item);
        }
        button.Flyout = menu;
        return button;
    }
    // Performs the customer menu action for this screen or workflow.
    private Button CustomerMenu()
    {
        var button = Ui.Button("Customer ▾", () => { });
        var menu = new MenuFlyout();
        foreach (var customer in model.Customers)
        {
            var item = new MenuItem { Header = customer.Name };
            item.Click += (_, _) => { menu.Hide(); search.Text = customer.Name; };
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "No customers", IsEnabled = false });
        button.Flyout = menu;
        return button;
    }
    // Performs the filtered action for this screen or workflow.
    private IEnumerable<UiRecord> Filtered()
    {
        var query = Records.Where(r => r.Values.Values.Any(v => v.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)));
        query = filter switch {
            "Businesses" => query.Where(r => r["Business Name"].Length > 0), "Individuals" => query.Where(r => r["Business Name"].Length == 0),
            "Without GST" => query.Where(r => r["GST / VAT Number"].Length == 0), "With Outstanding" => query.Where(r => decimal.TryParse(r["Outstanding"], out var outstanding) && outstanding > 0), "Expired" => query.Where(r => DateTime.TryParse(r["Expiry Date"], out var expiry) && expiry < DateTime.Today),
            "GST Registered" => query.Where(r => r["GST / VAT Number"].Length > 0), "Services" => query.Where(r => r["Type"] == "Service"), "Products" => query.Where(r => r["Type"] == "Product"),
            "Low Stock" => query.Where(r => !HasUnlimitedStock(r) && decimal.TryParse(r["Stock"], out var s) && s <= 5), "Out of Stock" => query.Where(r => !HasUnlimitedStock(r) && decimal.TryParse(r["Stock"], out var s) && s <= 0),
            "Admin" or "User" => query.Where(r => r["Role"] == filter), "Paid" or "Partial" or "Unpaid" or "Overdue" => query.Where(r => r["Status"] == filter), _ => query };
        return sort switch { "Name A–Z" => query.OrderBy(r => r.Name), "Name Z–A" => query.OrderByDescending(r => r.Name), "Newest" => query.Reverse(), _ => query };
    }
    private string[] Headers => Documents ? ["Invoice / Customer", "Title", "Date", "Items", "Total", "Status", "Outstanding"] : kind == "Customer" ? ["Name / Business", "Phone", "Email", "GST / VAT No.", "Address", "Outstanding"] : kind == "Product" ? ["Name / Alias", "Price", "HSN/SAC", "Purchase Price", "Stock", "Tax Rate", "Expiry Date", "Description", "Default Discount", "Unit", "Storage Location", "Container Number", "Batch Number", "Manufacture Date", "Manufacturer Name", "Supplier Name", "SKU Code", "Notes"] : ["Username", "Role"];
    private readonly HashSet<string> hidden = [];
    // Performs the columns action for this screen or workflow.
    private void Columns()
    {
        var checks = Headers.Where(h => kind != "Product" || model.ProductFieldVisible(h)).Select(h => new CheckBox { Content = h, IsChecked = !hidden.Contains(h), IsEnabled = kind != "Product" || h is not ("Name / Alias" or "Price") }).ToArray();
        window.ShowOverlay("Show Columns", Ui.Stack(10, checks), Ui.Wrap(Ui.Button("Cancel", window.CloseOverlay), Ui.Button("Apply", () => { hidden.Clear(); foreach (var c in checks.Where(c => c.IsChecked != true)) hidden.Add(c.Content!.ToString()!); Refresh(); window.CloseOverlay(); }, true)));
    }
    // Performs the refresh action for this screen or workflow.
    private void Refresh()
    {
        var total = Records.Count();
        string[] tabNames = Documents ? ["All", "Paid", "Partial", "Unpaid", "Overdue"] : kind == "Customer" ? ["All", "Businesses", "Individuals", "GST Registered", "With Outstanding", "Without GST"] : kind == "Product" ? ["All", "Products", "Services", "Low Stock", "Out of Stock", "Expired"] : ["All", "Admin", "User"];
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var tab in tabNames)
        {
            var oldFilter = filter; filter = tab; var count = Filtered().Count(); filter = oldFilter;
            var chip = Ui.Button($"{tab} ({count})", () => { filter = tab; page = 0; Refresh(); }, tab == filter); chips.Children.Add(chip);
        }
        tabs.Content = new ScrollViewer { Content = chips, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        stats.Content = Documents ? DocumentStats(total) : kind == "User" ? UserStats(total) : kind == "Customer" ? Ui.Stats(("Total Customers", total.ToString(), "All customers", "#002E78"), ("Businesses", Records.Count(r => r["Business Name"].Length > 0).ToString(), "Registered businesses", "#4CAF50"), ("Individuals", Records.Count(r => r["Business Name"].Length == 0).ToString(), "Individual customers", "#673AB7"), ("GST Registered", Records.Count(r => r["GST / VAT Number"].Length > 0).ToString(), "With GST number", "#FF9800")) : Ui.Stats(($"Total {kind}s", total.ToString(), "Total items", "#002E78"), ("Products", Records.Count(r => r["Type"] == "Product").ToString(), "", "#4CAF50"), ("Services", Records.Count(r => r["Type"] == "Service").ToString(), "", "#673AB7"));
        var filtered = Filtered().ToArray();
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)pageSize)); page = Math.Clamp(page, 0, pages - 1);
        if (Documents && DocumentPageSummaryHost != null) DocumentPageSummaryHost.Content = Ui.Text($"Total: {filtered.Length}   ·   Page {page + 1}/{pages}", 12, color: Ui.Muted);
        var body = Ui.Stack(0); var columns = kind == "Product" ? ProductColumns() : kind == "Customer" ? CustomerColumns() : Documents ? DocumentColumns() : kind == "User" ? "44,*,220,160" : string.Join(",", new[] { "0", "56" }.Concat(Headers.Where(h => !hidden.Contains(h) && (kind != "Product" || model.ProductFieldVisible(h))).Select(_ => "*")).Append("160"));
        Control TableRow(UiRecord? record, int index)
        {
            if (kind == "Product") return ProductTableRow(record, index);
            if (kind == "Customer") return CustomerTableRow(record, index);
            if (Documents) return DocumentTableRow(record, index);
            if (kind == "User") return UserTableRow(record);
            var controls = new List<Control>();
            var checkbox = new CheckBox { IsChecked = record != null && selected.Contains(record.Id), IsVisible = record != null };
            checkbox.IsCheckedChanged += (_, _) => { if (record == null) return; if (checkbox.IsChecked == true) selected.Add(record.Id); else selected.Remove(record.Id); };
            controls.Add(checkbox); controls.Add(Ui.Text(record == null ? "SL. NO." : (index + 1).ToString(), 12));
            string[] values = record == null ? Headers : Documents ? [$"{record.Name}\n{record["Customer"]}", record["Title"].Length == 0 ? "—" : record["Title"], record["Date"], record["Items"], FormatMoney(record["Total"]), record["Status"], string.IsNullOrWhiteSpace(record["Outstanding"]) || record["Outstanding"] == "0" ? "—" : FormatMoney(record["Outstanding"])] : kind == "Customer" ? [$"{record.Name}\n{record["Business Name"]}", record["Phone"], record["Email"], record["GST / VAT Number"], record["Address"], record["Outstanding"].Length == 0 ? "—" : record["Outstanding"]] : kind == "Product" ? [$"{record.Name}\n{record["Alias Name (for invoice PDF)"]}", record["Sale Price"], record["HSN/SAC"], record["Purchase Price"], record["Stock"], record["Tax (%)"], record["Expiry Date"]] : [record.Name, record["Role"]];
            for (var i = 0; i < Headers.Length; i++) if (!hidden.Contains(Headers[i])) controls.Add(DocumentCell(record, values[i], i));
            controls.Add(record == null ? Ui.Text("Actions", 12, true) : DocumentActions(record));
            return new Border { Background = record == null && Documents ? Brush.Parse("#243447") : Ui.CardSurface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, Documents ? 12 : 8), Child = Ui.Columns(columns, controls.ToArray()) };
        }
        body.Children.Add(TableRow(null, 0));
        if (filtered.Length == 0) body.Children.Add(Ui.Empty(trash ? "Trash is empty" : $"No {kind.ToLower()}s found", search.Text?.Length > 0 ? "Try adjusting your search" : $"Add your first {kind.ToLower()} to get started"));
        else foreach (var (record, index) in filtered.Skip(page * pageSize).Take(pageSize).Select((r, i) => (r, page * pageSize + i))) body.Children.Add(TableRow(record, index));
        var sizes = new ComboBox { ItemsSource = new[] { 10, 25, 50, 100 }, SelectedItem = pageSize };
        sizes.SelectionChanged += (_, _) => { pageSize = (int)(sizes.SelectedItem ?? 10); page = 0; Refresh(); };
        var pager = kind == "User"
            ? UserPager(filtered.Length, pages)
            : Documents
            ? Ui.Columns("Auto,*,Auto", Ui.Wrap(Ui.Text("Rows per page:", 12), sizes), new Border(), Ui.Wrap(Ui.Button("‹ Previous", () => { page--; Refresh(); }), Ui.Button($"Page {page + 1} of {pages}", () => { }, true), Ui.Button("› Next", () => { page++; Refresh(); })))
            : Ui.Columns("*,Auto", Ui.Text($"Showing {(filtered.Length == 0 ? 0 : page * pageSize + 1)} to {Math.Min((page + 1) * pageSize, filtered.Length)} of {filtered.Length}", 12, color: Ui.Muted), Ui.Wrap(Ui.Text("Rows per page", 12), sizes, Ui.Button("‹", () => { page--; Refresh(); }), Ui.Text($"{page + 1} of {pages}", 12), Ui.Button("›", () => { page++; Refresh(); })));
        body.Children.Add(new Border { Padding = Documents ? new Thickness(20, 10, 20, 0) : new Thickness(16, 8), Child = pager });
        results.Content = Documents
            ? new ScrollViewer { Content = new Border { Padding = new Thickness(24, 0, 24, 0), MinWidth = 1120, Child = body }, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }
            : Ui.Card(new ScrollViewer { Content = new Border { MinWidth = kind == "Product" ? Math.Max(800, 200 + Headers.Count(h => !hidden.Contains(h) && model.ProductFieldVisible(h)) * 130) : kind == "Customer" ? 1060 : 700, Child = body }, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, 0);
    }

    // Builds the three summary cards shown above the user table.
    private Control UserStats(int total) => Ui.Stats(
        ("Total Users", total.ToString(), "All users", "#0D47A1"),
        ("Admin Users", Records.Count(record => record["Role"] == "Admin").ToString(), "Full access", "#9C27B0"),
        ("Regular Users", Records.Count(record => record["Role"] == "User").ToString(), "Standard access", "#2196F3"));

    // Builds a user table header or row with user-specific actions.
    private Control UserTableRow(UiRecord? record)
    {
        if (record == null)
            return new Border { Background = Brush.Parse("#FBF6FC"), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 10), Child = Ui.Columns("44,*,220,160", new CheckBox { IsEnabled = false }, Ui.Text("USER", 11, true, Ui.Muted), Ui.Text("ROLE", 11, true, Ui.Muted), Ui.Text("ACTIONS", 11, true, Ui.Muted)) };

        var isAdmin = record["Role"] == "Admin";
        if (isAdmin) selected.Remove(record.Id);
        var check = new CheckBox { IsChecked = !isAdmin && selected.Contains(record.Id), IsEnabled = !isAdmin, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(check, isAdmin ? "Administrator users cannot be deleted." : "Select user");
        check.IsCheckedChanged += (_, _) => { if (check.IsChecked == true) selected.Add(record.Id); else selected.Remove(record.Id); Refresh(); };
        var initial = record.Name.Length == 0 ? "?" : record.Name[..1].ToUpperInvariant();
        var initialText = Ui.Text(initial, 14, true, Brush.Parse("#9C27B0"));
        initialText.HorizontalAlignment = HorizontalAlignment.Center;
        initialText.VerticalAlignment = VerticalAlignment.Center;
        initialText.TextAlignment = TextAlignment.Center;
        var avatar = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Brush.Parse("#F0DDF8"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Child = initialText };
        var nameLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        nameLine.Children.Add(Ui.Text(record.Name, 14, true));
        if (record.Name == model.CurrentUsername) nameLine.Children.Add(new Border { Background = Brush.Parse("#E3F2FD"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Text("You", 10, true, Ui.Primary) });
        var user = Ui.Columns("34,10,*", avatar, new Border(), nameLine);
        var role = new Border { Background = Brush.Parse(record["Role"] == "Admin" ? "#F3E5F5" : "#E3F2FD"), CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 4), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Text(record["Role"], 11, false, record["Role"] == "Admin" ? Brush.Parse("#9C27B0") : Ui.Primary) };
        return new Border { Background = Ui.CardSurface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 10), Child = Ui.Columns("44,*,220,160", check, user, role, UserActions(record)) };
    }

    // Builds View, Edit, and Change Password actions for one user.
    private Control UserActions(UiRecord record)
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(IconAction("visibility", "View", () => window.ShowUserDetails(record, Refresh), "#4CAF50"));
        actions.Children.Add(IconAction("edit", "Edit", () => window.ShowUserEditor(record, Refresh), "#2196F3"));
        actions.Children.Add(IconAction("lock", "Change Password", () => window.ShowUserPassword(record), "#FF9800"));
        return actions;
    }

    // Builds selection actions and pagination for the user table.
    private Control UserPager(int count, int pages)
    {
        var bulk = Ui.Button($"☷  Bulk Actions ({selected.Count}) ▾", () => { });
        var menu = new MenuFlyout();
        var delete = new MenuItem { Header = "Delete selected", IsEnabled = selected.Count > 0 };
        delete.Click += (_, _) => window.Confirm("Delete Users", $"Delete {selected.Count} selected user(s)?", () => { foreach (var record in model.Users.Where(item => selected.Contains(item.Id)).ToArray()) model.DeleteRecord("User", record); selected.Clear(); Refresh(); });
        menu.Items.Add(delete);
        bulk.Flyout = menu;
        return Ui.Columns("Auto,20,*,Auto", bulk, new Border(), Ui.Text($"Showing {(count == 0 ? 0 : page * pageSize + 1)} to {Math.Min((page + 1) * pageSize, count)} of {count} users", 12, color: Ui.Muted), Ui.Wrap(Ui.Button("‹", () => { page--; Refresh(); }), Ui.Button((page + 1).ToString(), () => { }, true), Ui.Text($"of {pages}", 12), Ui.Button("›", () => { page++; Refresh(); })));
    }


    // Performs the document table row action for this screen or workflow.
    private Control DocumentTableRow(UiRecord? record, int index)
    {
        var controls = new List<Control>();
        var checkbox = new CheckBox { IsChecked = record != null && selected.Contains(record.Id), IsVisible = record != null, VerticalAlignment = VerticalAlignment.Center };
        checkbox.IsCheckedChanged += (_, _) => { if (record == null) return; if (checkbox.IsChecked == true) selected.Add(record.Id); else selected.Remove(record.Id); };
        controls.Add(record == null ? new CheckBox { IsEnabled = false, VerticalAlignment = VerticalAlignment.Center } : checkbox);
        controls.Add(record == null ? DocumentHeaderOrCell("Sl No", true) : new Border { Background = Brush.Parse("#E3F2FD"), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 4), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = Ui.Text((index + 1).ToString(), 12, true, Ui.Primary) });
        string[] values = record == null
            ? Headers
            : [$"{record.Name}\n{record["Customer"]}", record["Title"].Length == 0 ? "—" : record["Title"], record["Date"], record["Items"], FormatMoney(record["Total"]), record["Status"], string.IsNullOrWhiteSpace(record["Outstanding"]) || record["Outstanding"] == "0" ? "—" : FormatMoney(record["Outstanding"])];
        for (var i = 0; i < Headers.Length; i++)
            if (!hidden.Contains(Headers[i])) controls.Add(DocumentCell(record, values[i], i));
        controls.Add(record == null ? DocumentHeaderOrCell("Actions", true) : DocumentActions(record));

        return new Border
        {
            Background = record == null ? Brush.Parse("#26364C") : Ui.Canvas,
            BorderBrush = record == null ? Brush.Parse("#26364C") : Ui.Outline,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, record == null ? 10 : 8),
            MinHeight = record == null ? 46 : 48,
            Child = Ui.Columns(DocumentColumns(), controls.ToArray())
        };
    }

    // Performs the document header or cell action for this screen or workflow.
    private static Control DocumentHeaderOrCell(string text, bool header)
        => Ui.Text(text, header ? 12 : 13, header, header ? Brushes.White : Ui.TextColor);

    // Performs the document stats action for this screen or workflow.
    private Control DocumentStats(int total)
    {
        var paid = Records.Count(r => r["Status"] == "Paid");
        var partial = Records.Count(r => r["Status"] == "Partial");
        var unpaid = Records.Count(r => r["Status"] == "Unpaid");
        var overdue = Records.Count(r => r["Status"] == "Overdue");
        return Ui.Stats(($"Total {kind}s", total.ToString(), $"All {kind.ToLowerInvariant()}s", "#002E78"), ("Paid", paid.ToString(), "Fully settled", "#4CAF50"), ("Partial", partial.ToString(), "Part paid", "#FF9800"), ("Unpaid", unpaid.ToString(), "Awaiting payment", "#F44336"), ("Overdue", overdue.ToString(), "Needs attention", "#D32F2F"));
    }

    // Performs the document columns action for this screen or workflow.
    private string DocumentColumns()
    {
        var widths = new Dictionary<string, string> { ["Invoice / Customer"] = "1.45*", ["Title"] = ".65*", ["Date"] = ".75*", ["Items"] = ".55*", ["Total"] = ".9*", ["Status"] = ".85*", ["Outstanding"] = ".9*" };
        return string.Join(",", new[] { "40", "70" }.Concat(Headers.Where(h => !hidden.Contains(h)).Select(h => widths[h])).Append("250"));
    }


    // Performs the customer columns action for this screen or workflow.
    private string CustomerColumns()
    {
        var widths = new Dictionary<string, string>
        {
            ["Name / Business"] = "2.0*",
            ["Phone"] = "1.15*",
            ["Email"] = "1.65*",
            ["GST / VAT No."] = "1.5*",
            ["Address"] = "2.15*",
            ["Outstanding"] = "1.05*"
        };
        return string.Join(",", new[] { "56" }.Concat(Headers.Where(h => !hidden.Contains(h)).Select(h => widths[h])).Append("190"));
    }

    // Performs the customer table row action for this screen or workflow.
    private Control CustomerTableRow(UiRecord? record, int index)
    {
        var controls = new List<Control> { HeaderOrCell(record == null ? "SL. NO." : (index + 1).ToString(), record == null, false, record == null ? null : Ui.Muted) };
        string[] values = record == null
            ? Headers
            : [$"{record.Name}\n{record["Business Name"]}", record["Phone"], record["Email"], record["GST / VAT Number"], record["Address"], record["Outstanding"]];
        for (var i = 0; i < Headers.Length; i++)
            if (!hidden.Contains(Headers[i])) controls.Add(CustomerCell(record, Headers[i], values[i]));
        controls.Add(record == null ? HeaderOrCell("ACTIONS", true) : CustomerActions(record));
        return new Border
        {
            Background = record == null ? Ui.CardSurface : Brush.Parse("#FFF7FE"),
            BorderBrush = Ui.Outline,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, record == null ? 8 : 10),
            MinHeight = record == null ? 32 : 58,
            Child = Ui.Columns(CustomerColumns(), controls.ToArray())
        };
    }

    // Performs the customer cell action for this screen or workflow.
    private Control CustomerCell(UiRecord? record, string header, string value)
    {
        if (record == null) return HeaderOrCell(value.ToUpperInvariant(), true);
        return header switch
        {
            "Name / Business" => CustomerNameCell(record),
            "Outstanding" => HeaderOrCell(OutstandingText(value), false, true, OutstandingBrush(value)),
            _ => HeaderOrCell(TrimLong(value), false, false, Ui.Muted)
        };
    }

    // Performs the customer name cell action for this screen or workflow.
    private static Control CustomerNameCell(UiRecord record)
    {
        var business = record["Business Name"];
        var name = Ui.Text(record.Name, 13, true);
        name.TextWrapping = TextWrapping.NoWrap;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var subtitle = Ui.Text(string.IsNullOrWhiteSpace(business) ? record.Name : business, 11, color: Ui.Muted);
        subtitle.TextWrapping = TextWrapping.NoWrap;
        subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        var initials = Ui.Text(Initials(record.Name), 12, true, Brush.Parse("#FF7A00"));
        initials.HorizontalAlignment = HorizontalAlignment.Center;
        initials.VerticalAlignment = VerticalAlignment.Center;
        initials.TextAlignment = TextAlignment.Center;
        return new Border
        {
            ClipToBounds = true,
            Child = Ui.Columns("40,*",
                new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = Brush.Parse("#FFE5CC"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = initials
                },
                Ui.Stack(2, name, subtitle))
        };
    }

    // Performs the customer actions action for this screen or workflow.
    private Control CustomerActions(UiRecord record) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            PlainIconAction("visibility", "View", () => View(record), "#6E6E6E"),
            PlainIconAction("receipt_long", "Invoices", () => window.OpenInvoicesForCustomer(record.Name), "#6E6E6E"),
            PlainIconAction("account_balance_wallet", "Payment", () => window.OpenCustomerReport(record.Name), "#6E6E6E"),
            PlainIconAction("edit", "Edit", () => window.EditRecord(kind, Refresh, record), "#6E6E6E"),
            PlainIconAction("delete", "Delete", () => window.Confirm("Confirm Delete", $"Delete {record.Name}?", () => { Delete(record); Refresh(); }), "#D32F2F")
        }
    };

    // Performs the initials action for this screen or workflow.
    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    // Performs the trim long action for this screen or workflow.
    private static string TrimLong(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        return value.Length > 24 ? value[..21] + "…" : value;
    }

    // Performs the outstanding text action for this screen or workflow.
    private static string OutstandingText(string value) => decimal.TryParse(value, out var amount) && amount != 0 ? $"Rs. {amount:0.00}" : "—";
    // Performs the outstanding brush action for this screen or workflow.
    private static IBrush OutstandingBrush(string value) => decimal.TryParse(value, out var amount) && amount > 0 ? Brush.Parse("#F57C00") : Ui.Muted;


    // Performs the product columns action for this screen or workflow.
    private string ProductColumns()
    {
        var widths = new Dictionary<string, string>
        {
            ["Name / Alias"] = "2.2*",
            ["Price"] = "1.25*",
            ["HSN/SAC"] = "1.2*",
            ["Purchase Price"] = "1.55*",
            ["Stock"] = ".9*",
            ["Tax Rate"] = ".95*",
            ["Expiry Date"] = "1.25*"
        };
        return string.Join(",", new[] { "56" }.Concat(Headers.Where(h => !hidden.Contains(h) && (kind != "Product" || model.ProductFieldVisible(h))).Select(h => widths.GetValueOrDefault(h, "1.3*"))).Append("122"));
    }

    // Performs the product table row action for this screen or workflow.
    private Control ProductTableRow(UiRecord? record, int index)
    {
        var controls = new List<Control> { HeaderOrCell(record == null ? "SL. NO." : (index + 1).ToString(), record == null, false) };
        string[] values = record == null
            ? Headers
            : Headers.Select(h => h switch { "Name / Alias" => record.Name, "Price" => record["Sale Price"], "Stock" => ProductStock(record), "Tax Rate" => record["Tax (%)"], _ => record[h] }).ToArray();
        for (var i = 0; i < Headers.Length; i++)
            if (!hidden.Contains(Headers[i]) && model.ProductFieldVisible(Headers[i])) controls.Add(ProductCell(record, Headers[i], values[i]));
        controls.Add(record == null ? new Border() : ProductActions(record));
        return new Border
        {
            Background = record == null ? Ui.CardSurface : Brush.Parse("#FFF7FE"),
            BorderBrush = Ui.Outline,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, record == null ? 8 : 14),
            MinHeight = record == null ? 32 : 69,
            Child = Ui.Columns(ProductColumns(), controls.ToArray())
        };
    }

    // Performs the header or cell action for this screen or workflow.
    private static Control HeaderOrCell(string text, bool header, bool strong = false, IBrush? color = null, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var block = Ui.Text(text.Length == 0 ? "—" : text, header ? 11 : 13, header || strong, color ?? (header ? Ui.Muted : Ui.TextColor));
        block.VerticalAlignment = VerticalAlignment.Center;
        block.HorizontalAlignment = alignment;
        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        return new Border { ClipToBounds = true, Child = block };
    }

    // Performs the product cell action for this screen or workflow.
    private Control ProductCell(UiRecord? record, string header, string value)
    {
        if (record == null) return HeaderOrCell(value.ToUpperInvariant(), true);
        return header switch
        {
            "Name / Alias" => ProductNameCell(record),
            "Price" => HeaderOrCell(ProductMoney(value), false, true),
            "Purchase Price" => HeaderOrCell(ProductMoney(value, true), false, false, Ui.Muted),
            "Stock" => HeaderOrCell(value, false, false, null, HorizontalAlignment.Center),
            "Tax Rate" => HeaderOrCell(value.Length == 0 ? "—" : value + "%", false, false, null, HorizontalAlignment.Center),
            "Expiry Date" => HeaderOrCell(value.Length == 0 ? "—" : value, false, false, Ui.Muted),
            _ => HeaderOrCell(value.Length == 0 ? "—" : value, false, false, Ui.Muted)
        };
    }

    // Performs the product name cell action for this screen or workflow.
    private Control ProductNameCell(UiRecord record)
    {
        var type = record["Type"].Length == 0 ? "Product" : record["Type"];
        var badgeColor = type == "Service" ? "#FF7A00" : "#2E7D32";
        var badgeBack = type == "Service" ? "#FFE9D6" : "#E4F3E7";
        var alias = record["Alias Name (for invoice PDF)"];
        var children = new List<Control> { Ui.Text(record.Name, 13, false), new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 3), HorizontalAlignment = HorizontalAlignment.Left, Background = Brush.Parse(badgeBack), Child = Ui.Text(type, 11, true, Brush.Parse(badgeColor)) } };
        if (!model.ProductFieldVisible("Type")) children.RemoveAt(1);
        if (model.ProductFieldVisible("Alias Name") && !string.IsNullOrWhiteSpace(alias)) children.Add(Ui.Text("(" + alias + ")", 11, color: Ui.Muted));
        return Ui.Stack(4, children.ToArray());
    }

    // Performs the has unlimited stock action for this screen or workflow.
    private static bool HasUnlimitedStock(UiRecord record) => bool.TryParse(record["Unlimited stock"], out var unlimited) && unlimited;

    // Performs the product stock action for this screen or workflow.
    private static string ProductStock(UiRecord record) => HasUnlimitedStock(record) ? "∞" : record["Stock"];

    // Performs the product money action for this screen or workflow.
    private static string ProductMoney(string value, bool dashWhenZero = false)
    {
        if (!decimal.TryParse(value, out var amount)) return string.IsNullOrWhiteSpace(value) ? "—" : value;
        if (dashWhenZero && amount == 0) return "—";
        return $"Rs.{amount:0.00}";
    }

    // Performs the product actions action for this screen or workflow.
    private Control ProductActions(UiRecord record) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            PlainIconAction("visibility", "View", () => View(record), "#6E6E6E"),
            PlainIconAction("edit", "Edit", () => window.EditRecord(kind, Refresh, record), "#6E6E6E"),
            PlainIconAction("delete", "Delete", () => window.Confirm("Confirm Delete", $"Delete {record.Name}?", () => { Delete(record); Refresh(); }), "#D32F2F")
        }
    };

    // Performs the plain icon action action for this screen or workflow.
    private static Button PlainIconAction(string icon, string label, Action action, string color)
    {
        var button = Ui.Button(label, action);
        button.Content = Ui.Icon(icon, 18, Brush.Parse(color));
        button.Width = 26; button.Height = 26;
        button.MinWidth = 26; button.MinHeight = 26;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Tag = label;
        return button;
    }

    // Performs the format money action for this screen or workflow.
    private static string FormatMoney(string value) => decimal.TryParse(value, out var amount) ? $"Rs. {amount:0.00}" : value;

    // Performs the document cell action for this screen or workflow.
    private Control DocumentCell(UiRecord? record, string value, int index)
    {
        var header = Headers[index];
        if (record == null) return Ui.Text(value.ToUpperInvariant(), 11, true, Brushes.White);
        return header switch
        {
            "Invoice / Customer" => Ui.Stack(3, Ui.Text(record.Name, 13, true), Ui.Text("♙ " + record["Customer"] + "  ⓘ", 11, color: Ui.Muted)),
            "Items" => new Border { Background = Brush.Parse("#E3F2FD"), CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 3), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Text(value, 11, true, Brush.Parse("#1976D2")) },
            "Status" => StatusBadge(value),
            "Total" => Ui.Text(value, 13, true, Brush.Parse("#4CAF50")),
            "Outstanding" => Ui.Text(value, 13, true, value == "—" ? Ui.Muted : Brush.Parse("#F44336")),
            _ => Ui.Text(value, 12, false, value == "—" ? Ui.Muted : null)
        };
    }

    // Performs the status badge action for this screen or workflow.
    private static Control StatusBadge(string status)
    {
        var color = StatusBrush(status);
        var background = status switch { "Paid" => "#E8F5E9", "Partial" => "#FFF3E0", "Unpaid" => "#FFEBEE", "Overdue" => "#FFEBEE", _ => "#F5F5F5" };
        return new Border { Background = Brush.Parse(background), BorderBrush = color, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Text(status, 11, false, color) };
    }

    // Performs the status brush action for this screen or workflow.
    private static IBrush StatusBrush(string status) => status switch { "Paid" => Brush.Parse("#4CAF50"), "Partial" => Brush.Parse("#FF9800"), "Unpaid" => Brush.Parse("#F44336"), "Overdue" => Brush.Parse("#D32F2F"), _ => Ui.TextColor };

    // Performs the document actions action for this screen or workflow.
    private Control DocumentActions(UiRecord record)
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (trash)
        {
            actions.Children.Add(Ui.Button("Restore", () => { model.SetDocumentTrash(record, false); Refresh(); }));
            actions.Children.Add(DocumentOverflowMenu(record));
            return actions;
        }

        foreach (var button in new Control[]
        {
            IconAction("visibility", "View", () => window.ShowDocumentPreview(record), "#4CAF50"),
            IconAction("edit", "Edit", () => { if (model.LoadDocumentForEditing(record)) window.CloseOverlay(); }, "#2196F3"),
            IconAction("receipt_long", "Clone", () => { if (model.CloneDocumentForEditing(record)) window.CloseOverlay(); }, "#00A6A6"),
            IconAction("picture_as_pdf", "Preview PDF", async () => await window.ShowPdfPreviewAsync(record), "#FF9800"),
            //IconAction("visibility", "Preview PDF", async () => await window.ShowPdfPreviewAsync(record), "#7E57C2"),
            IconAction("print", "Print", async () => await window.PrintDocumentAsync(record), "#607D8B"),
            IconAction("account_balance_wallet", "Payment", () => window.ShowPayment(record), "#9C27B0"),
            DocumentOverflowMenu(record)
        }) actions.Children.Add(button);
        return actions;
    }

    // Performs the icon action action for this screen or workflow.
    private static Button IconAction(string icon, string label, Action action, string color)
    {
        var button = Ui.Button(label, action);
        button.Content = Ui.Icon(icon, 16, Brush.Parse(color));
        button.Width = 28; button.Height = 28;
        button.MinWidth = 28; button.MinHeight = 28;
        button.Padding = new Thickness(0);
        button.Background = new SolidColorBrush(Color.Parse(color), .12);
        button.BorderBrush = new SolidColorBrush(Color.Parse(color), .3);
        button.Tag = label;
        ToolTip.SetTip(button, label);
        return button;
    }

    // Performs the icon action action for this screen or workflow.
    private static Button IconAction(string icon, string label, Func<Task> action, string color)
    {
        var button = Ui.Button(label, async () => await action());
        button.Content = Ui.Icon(icon, 16, Brush.Parse(color));
        button.Width = 28; button.Height = 28;
        button.MinWidth = 28; button.MinHeight = 28;
        button.Padding = new Thickness(0);
        button.Background = new SolidColorBrush(Color.Parse(color), .12);
        button.BorderBrush = new SolidColorBrush(Color.Parse(color), .3);
        button.Tag = label;
        ToolTip.SetTip(button, label);
        return button;
    }

    // Performs the top icon action action for this screen or workflow.
    private static Button TopIconAction(string icon, string label, Action action)
    {
        var button = Ui.Button(label, action);
        button.Content = Ui.Icon(icon, 22, Brushes.White);
        button.Width = 28; button.Height = 28;
        button.MinWidth = 28; button.MinHeight = 28;
        button.Padding = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.Tag = label;
        return button;
    }

    // Performs the top icon action action for this screen or workflow.
    private static Button TopIconAction(string icon, string label, Func<Task> action)
    {
        var button = Ui.Button(label, async () => await action());
        button.Content = Ui.Icon(icon, 22, Brushes.White);
        button.Width = 28; button.Height = 28;
        button.MinWidth = 28; button.MinHeight = 28;
        button.Padding = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.Tag = label;
        return button;
    }

    // Performs the view action for this screen or workflow.
    private void View(UiRecord record)
    {
        if (kind == "Customer")
        {
            ShowCustomerView(record);
            return;
        }

        if (kind == "Product")
        {
            ShowProductView(record);
            return;
        }

        window.ShowOverlay($"{kind} Details", Ui.Stack(12, record.Values.Select(v => Ui.Stack(4, Ui.Text(v.Key, 12, color: Ui.Muted), Ui.Text(v.Value.Length == 0 ? "—" : v.Value))).ToArray()), Ui.Wrap(Ui.Button("Close", window.CloseOverlay), Ui.Button(Documents ? "Apply Payment" : "Edit", () => { if (Documents) window.ShowPayment(record); else window.EditRecord(kind, Refresh, record); }, true)));
    }


    // Performs the product read-only view dialog action for this screen or workflow.
    private void ShowProductView(UiRecord record)
    {
        var view = new ProductViewDialogModel
        {
            ProductName = ValueOrDash(record.Name),
            AliasName = ValueOrDash(record["Alias Name (for invoice PDF)"]),
            Description = ValueOrDash(record["Description"]),
            HsnSac = ValueOrDash(record["HSN/SAC"]),
            Price = ProductMoney(record["Sale Price"]),
            PurchasePrice = ProductMoney(record["Purchase Price"]),
            DefaultDiscount = ProductMoney(record["Default Discount"]),
            TaxRate = ValueOrDash(record["Tax (%)"]),
            PriceIncludesTax = bool.TryParse(record["Price includes tax"], out var includesTax) && includesTax,
            Type = string.IsNullOrWhiteSpace(record["Type"]) ? "Product" : record["Type"]
        };

        var badge = Ui.Button($"⚒  {view.Type}", () => { });
        badge.IsEnabled = false;
        badge.Classes.Add("outline");

        var footer = new Grid { Width = 698, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var delete = Ui.Button("🗑  Delete Product", () => window.Confirm("Confirm Delete", $"Delete {record.Name}?", () => { Delete(record); Refresh(); }));
        delete.Foreground = Brush.Parse("#F44336");
        delete.BorderBrush = Brush.Parse("#F44336");
        delete.Background = Brushes.Transparent;
        delete.Classes.Add("outline");
        var actions = Ui.Wrap(Ui.Button("Close", window.CloseOverlay), Ui.Button("✎  Edit", () => { window.CloseOverlay(); window.EditRecord(kind, Refresh, record); }, true));
        Grid.SetColumn(actions, 2);
        footer.Children.Add(delete);
        footer.Children.Add(actions);

        window.ShowOverlay("View Product", new ProductViewDialog(view), footer, width: 760, headerAccessory: badge);
    }

    // Performs the empty product value formatting action for this screen or workflow.
    private static string ValueOrDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    // Performs the customer read-only view dialog action for this screen or workflow.
    private void ShowCustomerView(UiRecord record)
    {
        var view = new CustomerViewDialogModel();
        void Add(string label, string icon, string key, int maxLength)
        {
            var value = record[key].Length == 0 ? "—" : record[key];
            view.Fields.Add(new CustomerViewFieldModel(label, icon, value, maxLength > 0 && value != "—" ? $"{value.Length}/{maxLength}" : ""));
        }

        view.Fields.Add(new CustomerViewFieldModel("Name", "👤", record.Name.Length == 0 ? "—" : record.Name, ""));
        Add("Business Name", "▣", "Business Name", 100);
        Add("Email", "✉", "Email", 0);
        Add("Phone", "☎", "Phone", 12);
        Add("GST / VAT Number", "▣", "GST / VAT Number", 50);
        Add("Address", "●", "Address", 100);
        window.ShowOverlay("", new CustomerViewDialog(view), Ui.Button("Close", window.CloseOverlay, true), width: 600);
    }
    // Performs the more menu action for this screen or workflow.
    private Button MoreMenu()
    {
        var button = Ui.Button(Documents ? "⋯" : "⋯ More", () => { });
        var menu = new MenuFlyout();
        void Add(string title, Action action)
        {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => { menu.Hide(); action(); };
            menu.Items.Add(item);
        }
        Add("Export PDF", async () => await ExportDocumentsPdf());
        Add("Delete selected", DeleteSelected);
        Add($"Delete All {kind}s", () => window.Confirm("Confirm Delete", $"{(trash && Documents ? "Permanently delete" : "Delete")} all {kind.ToLower()}s?", () => { foreach (var record in Records.ToArray()) Delete(record); Refresh(); }));
        button.Flyout = menu;
        return button;
    }


    // Performs the document overflow menu action for this screen or workflow.
    private Button DocumentOverflowMenu(UiRecord record)
    {
        var button = Ui.Button("⋯", () => { });
        button.Width = 28; button.Height = 28;
        button.MinWidth = 28; button.MinHeight = 28;
        button.Padding = new Thickness(0);
        var menu = new MenuFlyout();
        void Add(string title, string icon, Action? action)
        {
            var item = new MenuItem { Header = title, IsEnabled = action != null };
            item.Icon = Ui.Icon(icon, 18, title.Contains("Trash") ? Brush.Parse("#F44336") : Ui.Accent);
            item.Click += (_, _) => { menu.Hide(); action?.Invoke(); };
            menu.Items.Add(item);
        }
        Add("Duplicate", "receipt_long", () => model.Status = $"Duplicate {kind.ToLowerInvariant()} is not yet migrated.");
        Add(trash ? "Delete Permanently" : "Move to Trash", "delete", () => window.Confirm("Confirm Delete", $"Delete {record.Name}?", () => { Delete(record); Refresh(); }));
        button.Flyout = menu;
        return button;
    }

    // Performs the action menu action for this screen or workflow.
    private Button ActionMenu(UiRecord record)
    {
        var button = Ui.Button("⋯", () => { });
        var menu = new MenuFlyout();
        void Add(string title, Action? action)
        {
            var item = new MenuItem { Header = title, IsEnabled = action != null };
            item.Click += (_, _) => { menu.Hide(); action?.Invoke(); };
            menu.Items.Add(item);
        }
        Add("View", () => View(record));
        Add("Edit", () => { if (Documents) { if (model.LoadDocumentForEditing(record)) window.CloseOverlay(); } else window.EditRecord(kind, Refresh, record); });
        Add(Documents ? (trash ? "Delete Permanently" : "Move to Trash") : "Delete", () => window.Confirm("Confirm Delete", $"Delete {record.Name}?", () => { Delete(record); Refresh(); }));
        Add("Export PDF", Documents ? async () => await ExportDocumentPdf(record) : null);
        button.Flyout = menu;
        return button;
    }
    // Performs the delete action for this screen or workflow.
    private void Delete(UiRecord record)
    {
        if (!Documents) model.DeleteRecord(kind, record);
        else if (trash) model.DeleteDocumentPermanently(record);
        else model.SetDocumentTrash(record, true);
    }
    // Performs the delete selected action for this screen or workflow.
    private void DeleteSelected() => window.Confirm("Delete Selected", $"{(trash && Documents ? "Permanently delete" : "Delete")} {selected.Count} selected records?", () => { foreach (var record in Records.Where(r => selected.Contains(r.Id)).ToArray()) Delete(record); selected.Clear(); Refresh(); });
    // Performs the import action for this screen or workflow.
    private void Import()
    {
        if (kind is "Product" or "Customer")
        {
            var cancel = Ui.Button("Cancel", window.CloseOverlay);
            cancel.Classes.Add("text");
            var sample = Ui.Button("Download Sample", async () => await DownloadSampleCsv());
            var choose = Ui.Button("Choose File", async () => await ChooseCsvFile(), true);
            choose.Content = Ui.Columns("Auto,8,Auto", Ui.Icon("folder", 18, Brushes.White), new Border(), Ui.Text("Choose File", 13, true, Brushes.White));
            window.ShowOverlay(
                $"Import {kind}s from CSV",
                kind == "Product" ? ProductImportGuide() : CustomerImportGuide(),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { cancel, sample, choose }
                },
                width: kind == "Product" ? 665 : 645,
                leadingIcon: Ui.Icon("upload_file", 22, Ui.Primary));
            return;
        }

        var columns = kind == "Customer"
            ? "name (required), email, phone, address, business_name, tax_number"
            : "name (required), price (required), hsn_code, description, tax_rate, stock, type, default_discount, purchase_price, alias_name, unit, unlimited_stock, price_includes_tax, storage_location, container_number, batch_number, expiry_date, manufacture_date, manufacture_name, supplier_name, sku_code, notes";
        window.ShowOverlay($"Import {kind}s from CSV", Ui.Stack(16, Ui.Text("CSV columns", 16, true), Ui.Text(columns), Ui.Button("Download Sample CSV", async () => await DownloadSampleCsv()), Ui.Button("Choose File", async () => await ChooseCsvFile())));
    }

    // Builds the customer CSV requirements and validation notes shown before file selection.
    private static Control CustomerImportGuide()
    {
        (string Column, bool Required, string Description)[] rows =
        [
            ("name", true, "Customer full name"),
            ("email", false, "Email address"),
            ("phone", false, "Phone number"),
            ("address", false, "Full address"),
            ("business_name", false, "Company / business name"),
            ("tax_number", false, "Tax / VAT / GSTIN number")
        ];
        var notes = new[]
        {
            "Maximum 200 rows per import.",
            "Duplicates are detected by email or phone. You will be asked to overwrite or skip each one.",
            "Rows missing a name are skipped and reported at the end.",
            "UTF-8 encoding recommended. Excel BOM is handled automatically."
        };
        var noteList = Ui.Stack(7);
        foreach (var note in notes)
            noteList.Children.Add(Ui.Columns("18,*", Ui.Icon("info_outline", 15, Brush.Parse("#607D8B")), Ui.Text(note, 12)));
        return Ui.Stack(14,
            CsvRequirementsTable(rows, "224,104,*"),
            noteList);
    }

    // Builds the product CSV requirements table shown before choosing an import file.
    private static Control ProductImportGuide()
    {
        (string Column, bool Required, string Description)[] rows =
        [
            ("name", true, "Product name"),
            ("price", true, "Unit price (numeric)"),
            ("hsn_code", false, "HSN / SAC code"),
            ("description", false, "Short description"),
            ("tax_rate", false, "Tax % (0–100), default 0"),
            ("stock", false, "Stock quantity, default 0"),
            ("type", false, "\"product\" or \"service\", default product"),
            ("default_discount", false, "Flat discount amount (currency), default 0"),
            ("purchase_price", false, "Cost price (numeric), default 0"),
            ("alias_name", false, "Local-language display name for PDFs"),
            ("unit", false, "Unit of measure (e.g. kg, bag, pcs), default pcs"),
            ("unlimited_stock", false, "1/true for unlimited stock, default 0"),
            ("price_includes_tax", false, "1/true if price already includes tax, default 0"),
            ("storage_location", false, "Warehouse/shelf location"),
            ("container_number", false, "Container/box number")
        ];

        return CsvRequirementsTable(rows, "208,106,*");
    }

    // Creates a consistent three-column CSV schema table for import dialogs.
    private static Control CsvRequirementsTable((string Column, bool Required, string Description)[] rows, string columns)
    {
        var table = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            RowDefinitions = new RowDefinitions(string.Join(',', Enumerable.Repeat("30", rows.Length + 1)))
        };

        void AddCell(int row, int column, string text, bool header = false, IBrush? color = null)
        {
            var label = Ui.Text(text, header ? 12 : 11.5, header, color);
            label.TextWrapping = TextWrapping.NoWrap;
            var cell = new Border
            {
                Padding = new Thickness(8, 4),
                Background = header ? Brushes.White : Brushes.Transparent,
                BorderBrush = Ui.Outline,
                BorderThickness = new Thickness(column == 0 ? 0 : 1, row == 0 ? 0 : 1, 0, 0),
                Child = label
            };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            table.Children.Add(cell);
        }

        AddCell(0, 0, "Column", true);
        AddCell(0, 1, "Required", true);
        AddCell(0, 2, "Description", true);
        for (var index = 0; index < rows.Length; index++)
        {
            var item = rows[index];
            AddCell(index + 1, 0, item.Column);
            AddCell(index + 1, 1, item.Required ? "Yes" : "No", color: item.Required ? Brush.Parse("#E53935") : Ui.Muted);
            AddCell(index + 1, 2, item.Description);
        }

        return Ui.Stack(12,
            Ui.Text("Your CSV file must use the following column headers (exact spelling, any order):", 13),
            new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = Ui.Outline,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Child = table
            });
    }

    // Performs the export action for this screen or workflow.
    private void Export()
    {
        if (kind == "Product")
        {
            var currentCount = Filtered().Skip(page * pageSize).Take(pageSize).Count();
            var totalCount = Records.Count();
            var cancel = Ui.Button("Cancel", window.CloseOverlay); cancel.Classes.Add("text");
            var current = Ui.Button("Current Page", async () => await ExportProductsPdf(true));
            var all = Ui.Button("All Products", async () => await ExportProductsPdf(false), true);
            all.Background = Brush.Parse("#6D4CB3");
            window.ShowOverlay(
                "Export to PDF",
                Ui.Text($"Export the current page ({currentCount} product{(currentCount == 1 ? "" : "s")}) or all {totalCount} products?", 13),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { cancel, current, all } },
                width: 420,
                prominentHeader: true);
            return;
        }
        var currentPage = new RadioButton { Content = "Current Page", IsChecked = true, GroupName = "export" };
        var allRecords = new RadioButton { Content = "All Records", GroupName = "export" };
        window.ShowOverlay("Export", Ui.Stack(12, Ui.Text("Export records"), currentPage, allRecords, Ui.Button("Export CSV", async () => await ExportCsv(currentPage.IsChecked == true)), Ui.Button("Export PDF", Documents ? async () => await ExportDocumentsPdf() : null)));
    }

    // Exports either the visible product page or the complete active product catalog as a PDF.
    private async Task ExportProductsPdf(bool currentPageOnly)
    {
        var records = (currentPageOnly ? Filtered().Skip(page * pageSize).Take(pageSize) : Records).ToArray();
        if (records.Length == 0)
        {
            model.Status = "No products are available to export.";
            window.CloseOverlay();
            return;
        }
        try
        {
            var bytes = model.ExportRecordsPdf("Products", records);
            if (OperatingSystem.IsMacOS())
            {
                var path = FilePickerHelpers.MacDownloadsPdfPath("products");
                await File.WriteAllBytesAsync(path, bytes);
                model.Status = $"Saved PDF to {path}";
            }
            else
            {
                var file = await window.StorageProvider.SaveFilePickerAsync(FilePickerHelpers.PdfSaveOptions("Export Products PDF", "products"));
                if (file == null) return;
                await using var stream = await file.OpenWriteAsync();
                await LedgerNest.Infrastructure.BackupStreamWriter.WriteAsync(stream, bytes);
                model.Status = $"Saved {file.Name}.";
            }
            window.CloseOverlay();
        }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, "Exporting products PDF");
            model.Status = $"Could not save products PDF. Log: {AppErrorLog.Path}";
        }
    }


    // Performs the export document pdf action for this screen or workflow.
    private async Task ExportDocumentPdf(UiRecord record)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var path = FilePickerHelpers.MacDownloadsPdfPath(record.Name);
                await File.WriteAllBytesAsync(path, model.ExportDocumentPdf(record));
                model.Status = $"Saved PDF to {path}";
                return;
            }
            var file = await window.StorageProvider.SaveFilePickerAsync(FilePickerHelpers.PdfSaveOptions($"Export {record.Name} PDF", record.Name.ToLowerInvariant()));
            if (file == null) return;
            await using (var stream = await file.OpenWriteAsync())
                await LedgerNest.Infrastructure.BackupStreamWriter.WriteAsync(stream, model.ExportDocumentPdf(record));
            model.Status = $"Saved {file.Name}.";
        }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, $"Exporting {kind} PDF {record.Name}");
            model.Status = $"Could not save PDF. Log: {AppErrorLog.Path}";
        }
    }

    // Performs the export documents pdf action for this screen or workflow.
    private async Task ExportDocumentsPdf()
    {
        var first = Filtered().FirstOrDefault();
        if (first == null) { window.ShowOverlay("Export PDF", Ui.Text($"No {kind.ToLowerInvariant()}s to export."), Ui.Button("Close", window.CloseOverlay, true)); return; }
        await ExportDocumentPdf(first);
    }

    // Performs the download sample csv action for this screen or workflow.
    private async Task DownloadSampleCsv()
    {
        var sample = kind == "Customer"
            ? "name,email,phone,address,business_name,tax_number\nSample Customer,sample@example.com,9876543210,Main Road,Sample Trading Co,29ABCDE1234F1Z5\n"
            : "name,price,hsn_code,description,tax_rate,stock,type,default_discount,purchase_price,alias_name,unit,unlimited_stock,price_includes_tax,storage_location,container_number,batch_number,expiry_date,manufacture_date,manufacture_name,supplier_name,sku_code,notes\nSample Product,199.00,9983,Sample item,18,25,product,0,120.00,Sample Alias,pcs,false,false,Rack A,CN-1,B-1,2027-03-31,2026-03-31,Sample Manufacturer,Sample Supplier,SKU-001,Imported sample\n";
        await SaveTextFile($"ledgernest-{kind.ToLowerInvariant()}-sample.csv", sample);
    }

    // Performs the choose csv file action for this screen or workflow.
    private async Task ChooseCsvFile()
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = $"Import {kind}s from CSV",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CSV files") { Patterns = ["*.csv"], MimeTypes = ["text/csv", "text/plain"] }]
        });
        if (files.Count == 0) return;
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var imported = model.ImportCsv(kind, await reader.ReadToEndAsync());
        Refresh();
        window.ShowOverlay("Import Complete", Ui.Stack(8, Ui.Text(imported == 0 ? model.Status : $"{model.Status} The table has been refreshed.")), Ui.Button("Close", window.CloseOverlay, true));
    }

    // Performs the export csv action for this screen or workflow.
    private async Task ExportCsv(bool currentPageOnly)
    {
        var records = currentPageOnly ? Filtered().Skip(page * pageSize).Take(pageSize) : Records;
        await SaveTextFile($"ledgernest-{kind.ToLowerInvariant()}s.csv", model.ExportCsv(kind, records));
    }

    // Performs the save text file action for this screen or workflow.
    private async Task SaveTextFile(string suggestedName, string content)
    {
        try
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = suggestedName,
                SuggestedFileName = suggestedName,
                DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType("CSV files") { Patterns = ["*.csv"], MimeTypes = ["text/csv", "text/plain"] }]
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(content);
            model.Status = $"Saved {suggestedName}.";
        }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, $"Saving CSV {suggestedName}");
            model.Status = $"Could not save file. Log: {AppErrorLog.Path}";
        }
    }
}
