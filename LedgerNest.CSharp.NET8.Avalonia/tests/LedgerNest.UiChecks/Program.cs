using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Application;
using LedgerNest.Desktop;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Text.Json;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException(message); }
    [STAThread]
    private static void Main(string[] args)
    {
        var output = args.FirstOrDefault() ?? "/tmp/invoiso-ui-captures";
        Directory.CreateDirectory(output);
        CheckTotals();
        CheckPersistence();
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model, Width = 1440, Height = 900 };
        window.Show();
        Check(window.Title == Branding.Name, "Window must use the application brand");
        void Settle() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        void Capture(string name)
        {
            Settle(); using var frame = window.CaptureRenderedFrame();
            Check(frame != null, $"No rendered frame for {name}"); frame!.Save(Path.Combine(output, name + ".png"));
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Text)), $"Blank screen: {name}");
        }
        Button FindButton(string text) => window.GetVisualDescendants().OfType<Button>().Last(b => b.IsVisible && (b.Content?.ToString() == text || b.Tag?.ToString() == text));
        void Click(string text) { Settle(); var button = FindButton(text); Check(button.IsEnabled, $"Disabled button: {text}"); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); button.Command?.Execute(button.CommandParameter); Settle(); }
        foreach (var route in MainWindowViewModel.Routes) { model.NavigateCommand.Execute(route); Capture(route.Replace(" ", "-").ToLowerInvariant()); }
        foreach (var settings in new[] { "Company Info", "Backup", "Users", "PDF Settings", "Invoice Settings", "Product Details", "Customize", "Accessibility", "Software Info" }) { Click(settings); Capture("settings-" + settings.Replace(" ", "-").ToLowerInvariant()); }
        model.NavigateCommand.Execute("Reports");
        foreach (var report in new[] { "Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report" }) { Click(report); Capture("reports-" + report.Replace(" ", "-").ToLowerInvariant()); }
        model.NavigateCommand.Execute("Customers"); Click("＋ New Customer"); Capture("customer-form");
        Click("Save Customer"); Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Name is required."), "Customer form must display required-field errors");
        var name = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Watermark == "Name"); name.Text = "Test Customer";
        var phone = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Watermark == "Phone"); phone.Text = "1234567890";
        Click("Save Customer"); Check(model.Customers.Count == 1, "Save customer must update the list");
        var user = FormCatalog.User(); user[0].Value = "review-user"; user[1].Value = "temporary-secret";
        Check(model.SaveRecord("User", user), "User form must validate");
        Check(!model.Users.Single().Values.ContainsKey("Password"), "User table must not retain or expose password text"); Capture("customers-populated");
        model.NavigateCommand.Execute("Products"); Click("＋ New Product"); Capture("product-form"); Click("Cancel");
        model.NavigateCommand.Execute("New Invoice"); Click("＋ Custom Item"); Capture("custom-item-form"); Click("Cancel");
        model.Lines.Add(new InvoiceLineViewModel { Name = "Test product", Price = 100, Quantity = 2, TaxRate = 18 }); Capture("invoice-populated");
        model.InvoiceOptions[0].Value = "Percentage"; model.InvoiceOptions[1].Value = "10";
        Check(model.Totals.Total == 212.4m, "Invoice discount must affect totals");
        model.NavigateCommand.Execute("Dashboard"); model.NavigateCommand.Execute("New Invoice"); Check(model.Lines.Count == 1 && model.InvoiceOptions[1].Value == "10", "Navigation must preserve the invoice draft");
        Check(model.SaveInvoice(), "Invoice must save"); Check(model.Invoices.Single()["Total"] == "212.40", "Saved total must match displayed total");
        foreach (var width in new[] { 1024, 768, 640 })
        {
            window.Width = width; window.Height = 768;
            foreach (var route in new[] { "Dashboard", "New Invoice", "Customers", "Settings", "Reports" }) { model.NavigateCommand.Execute(route); Capture($"{route.Replace(" ", "-").ToLowerInvariant()}-{width}"); }
        }
        model.NavigateCommand.Execute("New Invoice");
        window.Width = 1440; window.Height = 900; Settle();
        var splitter = window.GetVisualDescendants().OfType<GridSplitter>().Single();
        var paneGrid = (Grid)splitter.Parent!;
        var previousWidth = paneGrid.ColumnDefinitions[2].ActualWidth;
        splitter.Focus();
        window.KeyPressQwerty(Avalonia.Input.PhysicalKey.ArrowLeft, Avalonia.Input.RawInputModifiers.None); window.KeyReleaseQwerty(Avalonia.Input.PhysicalKey.ArrowLeft, Avalonia.Input.RawInputModifiers.None); Settle();
        Check(paneGrid.ColumnDefinitions[2].ActualWidth > previousWidth, "Divider must resize invoice panes with the keyboard");
        window.Width = 640; Settle();
        Check(!window.GetVisualDescendants().OfType<GridSplitter>().Any(), "Narrow invoice layout must stack panels");
        window.Width = 1440; Settle();
        Check(window.GetVisualDescendants().OfType<GridSplitter>().Count() == 1, "Wide layout must restore its divider after resizing");
        Capture("invoice-split-panels");
        window.Close();
        if (args.Length > 1) CompareScreenshots(output, args[1]);
        Console.WriteLine($"Passed {assertions} checks. Screenshots: {output}");
    }
    private static void CompareScreenshots(string output, string reference)
    {
        var comparisons = new List<object>();
        foreach (var path in Directory.EnumerateFiles(reference, "*.png"))
        {
            var actualPath = Path.Combine(output, Path.GetFileName(path)); if (!File.Exists(actualPath)) continue;
            using var expected = SKBitmap.Decode(path); using var actual = SKBitmap.Decode(actualPath);
            if (expected.Width != actual.Width || expected.Height != actual.Height) continue;
            var expectedPixels = expected.Pixels; var actualPixels = actual.Pixels; long changed = 0; double error = 0;
            for (var i = 0; i < expectedPixels.Length; i++)
            {
                var a = expectedPixels[i]; var b = actualPixels[i];
                var delta = Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
                if (delta != 0) changed++; error += delta / 3d;
            }
            comparisons.Add(new { Screen = Path.GetFileNameWithoutExtension(path), Width = actual.Width, Height = actual.Height, ChangedPixelPercent = Math.Round(changed * 100d / expectedPixels.Length, 2), MeanChannelError = Math.Round(error / expectedPixels.Length, 2), ExactMatch = changed == 0 });
        }
        File.WriteAllText(Path.Combine(output, "comparison.json"), JsonSerializer.Serialize(comparisons, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void CheckTotals()
    {
        var exclusive = InvoiceTotalsCalculator.Calculate([new(100, 2, TaxRatePercent: 18)]);
        Check(exclusive.Total == 236m, "Exclusive item tax");
        var inclusive = InvoiceTotalsCalculator.Calculate([new(118, 2, TaxRatePercent: 18, PriceIncludesTax: true)]);
        Check(inclusive.Subtotal == 200m && inclusive.Tax == 36m && inclusive.Total == 236m, "Inclusive item tax must not be charged twice");
        var global = InvoiceTotalsCalculator.Calculate([new(110, 2, TaxRatePercent: 18, PriceIncludesTax: true)], InvoiceTaxMode.Global, 10);
        Check(global.Subtotal == 200m && global.Total == 220m, "Global mode must back out the global rate");
        var discount = InvoiceTotalsCalculator.Calculate([new(100, 2, 10, true, 5, 10)], additionalCosts: 20, discountKind: InvoiceDiscountKind.Percent, discountValue: 10);
        Check(discount.ItemDiscount == 20m && discount.Total == 201.15m, "Per-unit and invoice discounts with additional costs");
        var clamp = InvoiceTotalsCalculator.Calculate([new(10, 1)], discountKind: InvoiceDiscountKind.Amount, discountValue: 20);
        Check(clamp.Total == 0, "Invoice total must not become negative");
    }

    private static void CheckPersistence()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ledgernest-ui-checks", Guid.NewGuid().ToString("N"), "ledgernest.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        var factory = new TestDbContextFactory(options);

        var model = new MainWindowViewModel(factory);
        var customer = FormCatalog.Customer();
        customer[0].Value = "Persisted Customer";
        customer[2].Value = "5551234567";
        Check(model.SaveRecord("Customer", customer), "Customer must save to SQLite");
        var product = FormCatalog.Product();
        product[1].Value = "Persisted Product";
        product[5].Value = "42.50";
        product[8].Value = "18";
        product[10].Value = "4";
        Check(model.SaveRecord("Product", product), "Product must save to SQLite");
        var customerCsv = model.ExportCsv("Customer");
        Check(customerCsv.Contains("name,phone,business_name,email,gstin,address"), "Customer export must include legacy CSV headers");
        Check(customerCsv.Contains("Persisted Customer"), "Customer export must include saved customers");
        var productCsv = model.ExportCsv("Product");
        Check(productCsv.Contains("name,price,stock,tax_rate,hsncode,description"), "Product export must include legacy CSV headers");
        Check(productCsv.Contains("Persisted Product") && productCsv.Contains("42.50"), "Product export must include saved products");
        Check(model.ImportCsv("Customer", "name,phone,business_name,email,gstin,address\n\"Comma, Customer\",4445556666,Comma Co,comma@example.com,GST-C,\"Street 1, City\"\n") == 1, "Customer CSV import must add quoted records");
        Check(model.ImportCsv("Product", "name,price,stock,tax_rate,hsncode,description\nImported Widget,12.75,9,5,HSN-55,CSV item\n") == 1, "Product CSV import must add products");
        model.InvoiceCustomer[0].Value = "Persisted Customer";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Persisted Product", Price = 42.50m, Quantity = 2, TaxRate = 18 });
        Check(model.SaveInvoice(), "Invoice must save to SQLite");
        var payment = FormCatalog.Payment();
        payment[0].Value = "40";
        payment[2].Value = "UPI";
        payment[4].Value = "txn-123";
        Check(model.ApplyPayment(model.Invoices.Single(), payment), "Payment must save to SQLite");
        Check(model.Invoices.Single()["Status"] == "Partial", "Partial payment must update invoice status");
        Check(model.Invoices.Single()["Outstanding"] == "60.30", "Partial payment must update outstanding balance");

        var reloaded = new MainWindowViewModel(factory);
        Check(reloaded.Customers.Any(c => c.Name == "Persisted Customer"), "Customers must reload from SQLite");
        Check(reloaded.Customers.Any(c => c.Name == "Comma, Customer" && c["Address"] == "Street 1, City"), "Imported customers must reload from SQLite");
        Check(reloaded.Products.Any(p => p.Name == "Persisted Product" && p["Sale Price"] == "42.5"), "Products must reload from SQLite");
        Check(reloaded.Products.Any(p => p.Name == "Imported Widget" && p["Sale Price"] == "12.75" && p["HSN/SAC"] == "HSN-55"), "Imported products must reload from SQLite");
        Check(reloaded.Invoices.Any(i => i["Customer"] == "Persisted Customer" && i["Total"] == "100.30" && i["Status"] == "Partial"), "Invoices must reload from SQLite");
        Check(reloaded.Payments.Any(p => p.Name == "INV-0001-R1" && p["Amount"] == "40.00" && p["Method"] == "UPI"), "Payments must reload from SQLite");

        var companySections = model.Settings["Company Info"];
        var companyFields = companySections[1].Fields.ToDictionary(f => f.Label);
        companyFields["Company Name"].Value = "LedgerNest Labs";
        companyFields["Phone"].Value = "9998887777";
        companyFields["Email"].Value = "hello@ledgernest.test";
        companyFields["GSTIN"].Value = "GST-123";
        Check(model.SaveSettings("Company Info"), "Company settings must save to SQLite");

        var invoiceGeneral = model.Settings["Invoice Settings"].Single(s => s.Title == "General").Fields.ToDictionary(f => f.Label);
        invoiceGeneral["Invoice Prefix"].Value = "LN-";
        invoiceGeneral["Starting Number"].Value = "27";
        Check(model.SaveSettings("Invoice Settings"), "Invoice settings must save to SQLite");

        var pdfSections = model.Settings["PDF Settings"];
        pdfSections[0].Fields[0].Value = "A5";
        pdfSections[1].Fields[0].Value = "Grid Classic";
        pdfSections[3].Fields[0].Value = "#0F766E";
        Check(model.SaveSettings("PDF Settings"), "PDF settings must save to SQLite");

        reloaded = new MainWindowViewModel(factory);
        var reloadedCompanyFields = reloaded.Settings["Company Info"][1].Fields.ToDictionary(f => f.Label);
        Check(reloadedCompanyFields["Company Name"].Value == "LedgerNest Labs" && reloadedCompanyFields["GSTIN"].Value == "GST-123", "Company info must reload from SQLite");
        var reloadedInvoiceGeneral = reloaded.Settings["Invoice Settings"].Single(s => s.Title == "General").Fields.ToDictionary(f => f.Label);
        Check(reloadedInvoiceGeneral["Invoice Prefix"].Value == "LN-" && reloadedInvoiceGeneral["Starting Number"].Value == "27", "Invoice settings must reload from SQLite");
        Check(reloaded.Settings["PDF Settings"][1].Fields[0].Value == "Grid Classic" && reloaded.Settings["PDF Settings"][3].Fields[0].Value == "#0F766E", "PDF settings must reload from SQLite");
    }

    private sealed class TestDbContextFactory(DbContextOptions<LedgerNestDbContext> options) : IDbContextFactory<LedgerNestDbContext>
    {
        public LedgerNestDbContext CreateDbContext() => new(options);
    }
}
