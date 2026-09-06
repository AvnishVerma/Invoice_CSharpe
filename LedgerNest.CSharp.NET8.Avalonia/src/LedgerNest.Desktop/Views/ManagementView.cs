using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Text;

namespace LedgerNest.Desktop.Views;

internal sealed class ManagementView : ContentControl
{
    private readonly MainWindowViewModel model;
    private readonly MainWindow window;
    private readonly string kind;
    private readonly ContentControl results = new();
    private readonly ContentControl stats = new();
    private readonly TextBox search = new() { MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Stretch };
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
        this.model = model; this.kind = kind; this.window = window;
        search.Watermark = Documents ? "Search by Invoice ID or Customer Name…" : kind == "Customer" ? "Search customers by name, phone, email, GST…" : kind == "Product" ? "Search products by name, alias, HSN/SAC, SKU…" : "Search users…";
        search.TextChanged += (_, _) => { page = 0; Refresh(); };
        var add = Ui.Button($"＋ New {kind}", () => { if (Documents) model.NavigateCommand.Execute("New Invoice"); else window.EditRecord(kind, Refresh); }, true); add.Classes.Add("material");
        var more = Ui.Button("⋯ More", () => window.ShowOverlay("More Actions", Ui.Stack(8, Ui.Button("Export PDF", async () => await ExportDocumentsPdf()), Ui.Button("Delete selected", DeleteSelected), Ui.Button($"Delete All {kind}s", () => window.Confirm("Confirm Delete", $"Delete all {kind.ToLower()}s?", () => { foreach (var record in Records.ToArray()) deleted.Add(record.Id); Refresh(); })))));
        var header = Ui.Header($"{kind} Management", kind == "Customer" ? "Manage your customers and contact details" : kind == "Product" ? "Manage your products and services" : "Manage users and access permissions", Ui.Button("↑ Import", Import), Ui.Button("↓ Export", Export), more, Ui.Button("↻", Refresh), add);
        var filterButton = Ui.Button("Filter ▾", () =>
        {
            string[] options = Documents ? ["All", "Paid", "Partial", "Unpaid", "Overdue"] : kind == "Customer" ? ["All", "Businesses", "Individuals", "GST Registered", "Without GST", "With Outstanding"] : kind == "Product" ? ["All", "Products", "Services", "Low Stock", "Out of Stock", "Expired"] : ["All", "Admin", "User"];
            window.ShowOverlay("Filter", Ui.Stack(8, options.Select(option => Ui.Button(option, () => { filter = option; page = 0; Refresh(); window.CloseOverlay(); })).ToArray()));
        });
        var sortButton = Ui.Button("Sort: Name A–Z ▾", () => window.ShowOverlay("Sort By", Ui.Stack(8, new[] { "Name A–Z", "Name Z–A", "Newest", "Oldest" }.Select(option => Ui.Button(option, () => { sort = option; Refresh(); window.CloseOverlay(); })).ToArray())));
        var toolbar = Ui.Card(Ui.Stack(10, Ui.Columns("*,10,Auto", search, new Border(), Ui.Wrap(filterButton, sortButton, Ui.Button("Columns ▾", Columns), Ui.Button("◉", () => stats.IsVisible = !stats.IsVisible))), tabs), 12);
        var stack = Ui.Stack(12, header, stats, toolbar, results);
        if (kind == "Product")
        {
            var banner = Ui.Card(Ui.Columns("*,Auto", Ui.Stack(4, Ui.Text("New: Customize product fields", 13, true, Ui.Primary), Ui.Text("Choose which fields show for a simpler catalog. Settings > Customize Product Details.", 12, color: Ui.Primary)), Ui.Button("Configure", () => model.NavigateCommand.Execute("Settings"))), 16); banner.Background = Brush.Parse("#EFF6FF"); stack.Children.Insert(0, banner);
        }
        if (Documents) foreach (var control in new Control[] { search, filterButton, sortButton, results }) if (control.Parent is Panel parent) parent.Children.Remove(control);
        Content = Documents
            ? Ui.Rows("Auto,*", Ui.AppBar($"{kind} Management", Ui.Button("↓", Export), Ui.Button("Trash", () => { trash = !trash; Refresh(); }), Ui.Button("⋯", more.Command == null ? null : () => more.Command.Execute(null)), Ui.Button("↻", Refresh)), new Border { Background = Brush.Parse("#FAFAFA"), Child = Ui.Rows("Auto,*", new Border { Padding = new Thickness(20), Child = Ui.Columns("360,12,Auto,*", search, new Border(), Ui.Wrap(Ui.Button("Customer", () => window.ShowOverlay("Select Customer", Ui.Stack(8, model.Customers.Select(c => Ui.Button(c.Name, () => { search.Text = c.Name; window.CloseOverlay(); })).ToArray()))), filterButton, sortButton), new Border()) }, results) })
            : Ui.Scroll(stack);
        Refresh();
    }
    private IEnumerable<UiRecord> Filtered()
    {
        var query = Records.Where(r => r.Values.Values.Any(v => v.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)));
        query = filter switch {
            "Businesses" => query.Where(r => r["Business Name"].Length > 0), "Individuals" => query.Where(r => r["Business Name"].Length == 0),
            "Without GST" => query.Where(r => r["GST / VAT Number"].Length == 0), "With Outstanding" => query.Where(r => decimal.TryParse(r["Outstanding"], out var outstanding) && outstanding > 0), "Expired" => query.Where(r => DateTime.TryParse(r["Expiry Date"], out var expiry) && expiry < DateTime.Today),
            "GST Registered" => query.Where(r => r["GST / VAT Number"].Length > 0), "Services" => query.Where(r => r["Type"] == "Service"), "Products" => query.Where(r => r["Type"] == "Product"),
            "Low Stock" => query.Where(r => decimal.TryParse(r["Stock"], out var s) && s <= 5), "Out of Stock" => query.Where(r => decimal.TryParse(r["Stock"], out var s) && s <= 0),
            "Admin" or "User" => query.Where(r => r["Role"] == filter), "Paid" or "Partial" or "Unpaid" or "Overdue" => query.Where(r => r["Status"] == filter), _ => query };
        return sort switch { "Name A–Z" => query.OrderBy(r => r.Name), "Name Z–A" => query.OrderByDescending(r => r.Name), "Newest" => query.Reverse(), _ => query };
    }
    private string[] Headers => Documents ? ["Invoice / Customer", "Date", "Items", "Total", "Status"] : kind == "Customer" ? ["Name / Business", "Phone", "Email", "GST / VAT No.", "Address", "Outstanding"] : kind == "Product" ? ["Name / Alias", "Price", "HSN/SAC", "Purchase Price", "Stock", "Tax Rate", "Expiry Date"] : ["Username", "Role"];
    private readonly HashSet<string> hidden = [];
    private void Columns()
    {
        var checks = Headers.Select(h => new CheckBox { Content = h, IsChecked = !hidden.Contains(h) }).ToArray();
        window.ShowOverlay("Show Columns", Ui.Stack(10, checks), Ui.Wrap(Ui.Button("Cancel", window.CloseOverlay), Ui.Button("Apply", () => { hidden.Clear(); foreach (var c in checks.Where(c => c.IsChecked != true)) hidden.Add(c.Content!.ToString()!); Refresh(); window.CloseOverlay(); }, true)));
    }
    private void Refresh()
    {
        var total = Records.Count();
        string[] tabNames = kind == "Customer" ? ["All", "Businesses", "Individuals", "GST Registered", "With Outstanding", "Without GST"] : kind == "Product" ? ["All", "Products", "Services", "Low Stock", "Out of Stock", "Expired"] : ["All", "Admin", "User"];
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var tab in tabNames)
        {
            var oldFilter = filter; filter = tab; var count = Filtered().Count(); filter = oldFilter;
            var chip = Ui.Button($"{tab} ({count})", () => { filter = tab; page = 0; Refresh(); }, tab == filter); chips.Children.Add(chip);
        }
        tabs.Content = new ScrollViewer { Content = chips, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        stats.Content = Documents ? null : kind == "Customer" ? Ui.Stats(("Total Customers", total.ToString(), "All customers", "#002E78"), ("Businesses", Records.Count(r => r["Business Name"].Length > 0).ToString(), "Registered businesses", "#4CAF50"), ("Individuals", Records.Count(r => r["Business Name"].Length == 0).ToString(), "Individual customers", "#673AB7"), ("GST Registered", Records.Count(r => r["GST / VAT Number"].Length > 0).ToString(), "With GST number", "#FF9800")) : Ui.Stats(($"Total {kind}s", total.ToString(), "Total items", "#002E78"), (kind == "Product" ? "Products" : "Admins", Records.Count(r => r[kind == "Product" ? "Type" : "Role"] == (kind == "Product" ? "Product" : "Admin")).ToString(), "", "#4CAF50"), (kind == "Product" ? "Services" : "Users", Records.Count(r => r[kind == "Product" ? "Type" : "Role"] == (kind == "Product" ? "Service" : "User")).ToString(), "", "#673AB7"));
        var filtered = Filtered().ToArray();
        if (Documents && filtered.Length == 0) { results.Content = Ui.Empty(trash ? "Trash is empty" : $"No {kind.ToLower()}s found", $"Create your first {kind.ToLower()} to see it here"); return; }
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)pageSize)); page = Math.Clamp(page, 0, pages - 1);
        var body = Ui.Stack(0); var columns = string.Join(",", new[] { "0", "56" }.Concat(Headers.Where(h => !hidden.Contains(h)).Select(_ => "*")).Append("160"));
        Control TableRow(UiRecord? record, int index)
        {
            var controls = new List<Control>();
            var checkbox = new CheckBox { IsChecked = record != null && selected.Contains(record.Id), IsVisible = record != null };
            checkbox.IsCheckedChanged += (_, _) => { if (record == null) return; if (checkbox.IsChecked == true) selected.Add(record.Id); else selected.Remove(record.Id); };
            controls.Add(checkbox); controls.Add(Ui.Text(record == null ? "SL. NO." : (index + 1).ToString(), 12));
            string[] values = record == null ? Headers : Documents ? [$"{record.Name}\n{record["Customer"]}", record["Date"], record["Items"], record["Total"], record["Status"]] : kind == "Customer" ? [$"{record.Name}\n{record["Business Name"]}", record["Phone"], record["Email"], record["GST / VAT Number"], record["Address"], record["Outstanding"].Length == 0 ? "—" : record["Outstanding"]] : kind == "Product" ? [$"{record.Name}\n{record["Alias Name (for invoice PDF)"]}", record["Sale Price"], record["HSN/SAC"], record["Purchase Price"], record["Stock"], record["Tax (%)"], record["Expiry Date"]] : [record.Name, record["Role"]];
            for (var i = 0; i < Headers.Length; i++) if (!hidden.Contains(Headers[i])) controls.Add(Ui.Text(record == null ? values[i].ToUpperInvariant() : values[i], record == null ? 11 : 12, record == null, record == null ? Ui.Muted : null));
            controls.Add(record == null ? Ui.Text("Actions", 12, true) : Ui.Wrap(Ui.Button(trash ? "Restore" : "View", () => { if (trash) { deleted.Remove(record.Id); Refresh(); } else View(record); }), Ui.Button("⋯", () => Actions(record))));
            return new Border { Background = Ui.CardSurface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 8), Child = Ui.Columns(columns, controls.ToArray()) };
        }
        body.Children.Add(TableRow(null, 0));
        if (filtered.Length == 0) body.Children.Add(Ui.Empty(trash ? "Trash is empty" : $"No {kind.ToLower()}s found", search.Text?.Length > 0 ? "Try adjusting your search" : $"Add your first {kind.ToLower()} to get started"));
        else foreach (var (record, index) in filtered.Skip(page * pageSize).Take(pageSize).Select((r, i) => (r, page * pageSize + i))) body.Children.Add(TableRow(record, index));
        var sizes = new ComboBox { ItemsSource = new[] { 10, 25, 50, 100 }, SelectedItem = pageSize };
        sizes.SelectionChanged += (_, _) => { pageSize = (int)(sizes.SelectedItem ?? 10); page = 0; Refresh(); };
        body.Children.Add(new Border { Padding = new Thickness(16, 8), Child = Ui.Columns("*,Auto", Ui.Text($"Showing {(filtered.Length == 0 ? 0 : page * pageSize + 1)} to {Math.Min((page + 1) * pageSize, filtered.Length)} of {filtered.Length}", 12, color: Ui.Muted), Ui.Wrap(Ui.Text("Rows per page", 12), sizes, Ui.Button("‹", () => { page--; Refresh(); }), Ui.Text($"{page + 1} of {pages}", 12), Ui.Button("›", () => { page++; Refresh(); }))) });
        results.Content = Ui.Card(new ScrollViewer { Content = new Border { MinWidth = Documents ? 850 : 700, Child = body }, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, 0);
    }
    private void View(UiRecord record) => window.ShowOverlay($"{kind} Details", Ui.Stack(12, record.Values.Select(v => Ui.Stack(4, Ui.Text(v.Key, 12, color: Ui.Muted), Ui.Text(v.Value.Length == 0 ? "—" : v.Value))).ToArray()), Ui.Wrap(Ui.Button("Close", window.CloseOverlay), Ui.Button(Documents ? "Apply Payment" : "Edit", () => { if (Documents) window.ShowPayment(record); else window.EditRecord(kind, Refresh, record); }, true)));
    private void Actions(UiRecord record) => window.ShowOverlay("Actions", Ui.Stack(8, Ui.Button("View", () => View(record)), Ui.Button("Edit", Documents ? null : () => window.EditRecord(kind, Refresh, record)), Ui.Button(Documents ? "Move to Trash" : "Delete", () => window.Confirm("Confirm Delete", $"Delete {record.Name}?", () => { deleted.Add(record.Id); Refresh(); })), Ui.Button("Export PDF", Documents ? async () => await ExportDocumentPdf(record) : null)));
    private void DeleteSelected() => window.Confirm("Delete Selected", $"Delete {selected.Count} selected records?", () => { deleted.UnionWith(selected); selected.Clear(); Refresh(); });
    private void Import()
    {
        var columns = kind == "Customer" ? "name (required), phone (required), business_name, email, gstin, address" : "name (required), price (required), stock, tax_rate, hsncode, description";
        window.ShowOverlay($"Import {kind}s from CSV", Ui.Stack(16, Ui.Text("CSV columns", 16, true), Ui.Text(columns), Ui.Button("Download Sample CSV", async () => await DownloadSampleCsv()), Ui.Button("Choose File", async () => await ChooseCsvFile())));
    }

    private void Export()
    {
        var currentPage = new RadioButton { Content = "Current Page", IsChecked = true, GroupName = "export" };
        var allRecords = new RadioButton { Content = "All Records", GroupName = "export" };
        window.ShowOverlay("Export", Ui.Stack(12, Ui.Text("Export records"), currentPage, allRecords, Ui.Button("Export CSV", async () => await ExportCsv(currentPage.IsChecked == true)), Ui.Button("Export PDF", Documents ? async () => await ExportDocumentsPdf() : null)));
    }


    private async Task ExportDocumentPdf(UiRecord record)
    {
        var file = await window.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = $"Export {record.Name} PDF",
            SuggestedFileName = $"{record.Name.ToLowerInvariant()}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = [new FilePickerFileType("PDF files") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }]
        });
        if (file == null) return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(model.ExportDocumentPdf(record));
        window.ShowOverlay("PDF Exported", Ui.Text($"Saved {file.Name}."), Ui.Button("Close", window.CloseOverlay, true));
    }

    private async Task ExportDocumentsPdf()
    {
        var first = Filtered().FirstOrDefault();
        if (first == null) { window.ShowOverlay("Export PDF", Ui.Text($"No {kind.ToLowerInvariant()}s to export."), Ui.Button("Close", window.CloseOverlay, true)); return; }
        await ExportDocumentPdf(first);
    }

    private async Task DownloadSampleCsv()
    {
        var sample = kind == "Customer"
            ? "name,phone,business_name,email,gstin,address\nSample Customer,9876543210,Sample Trading Co,sample@example.com,29ABCDE1234F1Z5,Main Road\n"
            : "name,price,stock,tax_rate,hsncode,description\nSample Product,199.00,25,18,9983,Sample item\n";
        await SaveTextFile($"ledgernest-{kind.ToLowerInvariant()}-sample.csv", sample);
    }

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

    private async Task ExportCsv(bool currentPageOnly)
    {
        var records = currentPageOnly ? Filtered().Skip(page * pageSize).Take(pageSize) : Records;
        await SaveTextFile($"ledgernest-{kind.ToLowerInvariant()}s.csv", model.ExportCsv(kind, records));
    }

    private async Task SaveTextFile(string suggestedName, string content)
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
        window.ShowOverlay("Export Complete", Ui.Stack(8, Ui.Text($"Saved {suggestedName}.")), Ui.Button("Close", window.CloseOverlay, true));
    }
}
