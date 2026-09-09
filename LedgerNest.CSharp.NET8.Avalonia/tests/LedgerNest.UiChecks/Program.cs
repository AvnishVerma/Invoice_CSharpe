using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Styling;
using LedgerNest.Application;
using LedgerNest.Desktop;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        CheckServiceTotals();
        CheckAdministratorGuards();
        CheckPasswordMigration();
        CheckSessionInvalidation();
        CheckRejectedDatabaseRestores();
        CheckRejectedJsonRestores();
        CheckJsonRestoreRollback();
        CheckLockedDatabaseRestore();
        CheckCommittedRestoreReloadFailure();
        CheckCommittedJsonRestoreReloadFailure();
        CheckPendingOperationSession();
        CheckInvoiceSnapshots();
        CheckInvoiceEditing();
        CheckPersistence();
        CheckDocumentTypes();
        CheckDocumentNumbering();
        CheckDocumentTrash();
        CheckRecordDeletion();
        CheckTaxReport();
        CheckReceivablesLifecycle();
        CheckFormRoundTrips();
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
        foreach (var type in new[] { "Invoice", "Quotation", "Receipt" })
        {
            model.NavigateCommand.Execute(type == "Invoice" ? "Invoices" : type + "s");
            Click($"＋ New {type}");
            Check(model.InvoiceDetails[0].Value == type, $"New {type} action must select its document type");
            Check(FindButton($"Create {type} (Ctrl+S)") != null, $"Create action must label {type}");
            Capture($"create-{type.ToLowerInvariant()}");
        }
        model.StartDocument("Invoice");
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
        model.NavigateCommand.Execute("Invoices");
        Click("⋯"); Click("Move to Trash"); Click("Confirm");
        Check(!model.ActiveInvoices.Any(), "Move to Trash action must hide the invoice");
        Click("Trash"); Capture("invoice-trash");
        Click("Restore");
        Check(model.ActiveInvoices.Count() == 1, "Restore action must return the invoice to active records");

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
        model.SetThemeMode("Dark");
        global::Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        model.NavigateCommand.Execute("Dashboard");
        Capture("dashboard-dark");
        model.NavigateCommand.Execute("Settings");
        Capture("settings-dark");
        var editPath = Path.Combine(Path.GetTempPath(), $"ledgernest-edit-ui-{Guid.NewGuid():N}.db");
        var editFactory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={editPath}").Options);
        var editModel = new MainWindowViewModel(editFactory, editPath);
        editModel.Lines.Add(new InvoiceLineViewModel { Name = "Editable service", Price = 100, Quantity = 1 });
        Check(editModel.SaveInvoice(), "UI edit fixture must save");
        Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        window.DataContext = editModel;
        Check(ReferenceEquals(window.DataContext, editModel), "Live window must accept replacement model without reparenting errors");
        model.Status = "OLD MODEL STATUS";
        model.NavigateCommand.Execute("Customers");
        Settle();
        Check(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "OLD MODEL STATUS"), "Old model events must not update the replacement shell");
        Check(!editModel.CanAccessWorkspace, "Database-backed startup must require authentication");
        Capture("startup-login");
        editModel.NavigateCommand.Execute("New Invoice");
        window.KeyPressQwerty(Avalonia.Input.PhysicalKey.S, Avalonia.Input.RawInputModifiers.Control);
        window.KeyReleaseQwerty(Avalonia.Input.PhysicalKey.S, Avalonia.Input.RawInputModifiers.Control);
        window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Escape, Avalonia.Input.RawInputModifiers.None);
        window.KeyReleaseQwerty(Avalonia.Input.PhysicalKey.Escape, Avalonia.Input.RawInputModifiers.None);
        Settle();
        Check(editModel.Invoices.Count == 1 && window.GetVisualDescendants().OfType<Button>().Any(b => b.Content?.ToString() == "Login"), "Locked navigation and shortcuts must preserve login and avoid saving");
        Check(editModel.SignIn("admin", "admin") && !editModel.CanAccessWorkspace, "Default administrator must remain locked pending password change");
        Click("Cancel");
        Check(window.GetVisualDescendants().OfType<TextBox>().Any(t => t.Watermark == "Current Password"), "Mandatory password change cannot be dismissed");
        foreach (var field in window.GetVisualDescendants().OfType<TextBox>())
        {
            if (field.Watermark == "Current Password") field.Text = "admin";
            if (field.Watermark is "New Password (min 8 characters)" or "Confirm New Password") field.Text = "updated-admin-password";
        }
        Click("Change Password");
        Check(editModel.CanAccessWorkspace && !editModel.RequiresPasswordChange, "Successful password change must unlock workspace");
        editModel.NavigateCommand.Execute("Invoices");
        Click("⋯"); Click("Edit"); Capture("invoice-edit");
        Check(editModel.IsEditingDocument && editModel.Lines.Single().Name == "Editable service", "Edit action must load the saved document");
        var editorField = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Watermark == "Customer Name *");
        editorField.Text = "Unsaved theme draft";
        Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Capture("invoice-edit-dark");
        Check(((Avalonia.Media.ISolidColorBrush)window.Background!).Color.R < 64, "Dark mode must darken the window canvas");
        var itemLabel = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Editable service");
        Check(((Avalonia.Media.ISolidColorBrush)itemLabel.Foreground!).Color.R > 190, "Dark mode must use readable invoice text");
        Check(window.GetVisualDescendants().Contains(editorField) && editorField.Text == "Unsaved theme draft", "Theme changes must preserve the editor control and unsaved text");
        Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        Capture("invoice-edit-light");
        Check(((Avalonia.Media.ISolidColorBrush)window.Background!).Color.R > 240 && ((Avalonia.Media.ISolidColorBrush)itemLabel.Foreground!).Color.R < 32, "Returning to light mode must restore canvas and text colors");

        Click("Save Invoice (Ctrl+S)");
        Check(editModel.Invoices.Count == 1 && editModel.LastSavedDocument?.Name == "00000001", "UI save must update the existing document");
        Click("Create New Invoice");
        Check(!editModel.IsEditingDocument && editModel.Lines.Count == 0, "Success screen must start a fresh invoice");
        var sessionUser = FormCatalog.User();
        sessionUser[0].Value = "sidebar-user"; sessionUser[1].Value = "session-password";
        Check(editModel.SaveRecord("User", sessionUser) && editModel.SignIn("sidebar-user", "session-password"), "Sidebar fixture user must sign in");
        Settle();
        Check(editModel.CurrentRole == "User" && window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "sidebar-user"), "Sidebar must show signed-in account and role without navigating");
        Click("⇥");
        Check(editModel.CurrentUsername == null && editModel.CurrentRole == "" && !editModel.RequiresPasswordChange, "Logout button must clear all session identity");
        Check(!editModel.CanAccessWorkspace && window.GetVisualDescendants().OfType<Button>().Any(b => b.Content?.ToString() == "Login") && !window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "sidebar-user"), "Logout must remove previous sidebar identity");
        FormField[] signedOutChange = [new("Current Password", "session-password"), new("New Password", "replacement-password"), new("Confirm", "replacement-password")];
        Check(!editModel.ChangeCurrentPassword(signedOutChange), "Signed-out account must not retain password-change authority");
        Check(editModel.SignIn("admin", "updated-admin-password") && editModel.CurrentRole == "Admin", "Session may switch to another account");
        using (var db = editFactory.CreateDbContext())
        {
            db.Users.Single(u => u.Username == "admin").PasswordChanged = false;
            db.SaveChanges();
        }
        editModel.NavigateCommand.Execute("Customers");
        Settle();
        Check(!editModel.CanAccessWorkspace && editModel.CurrentUsername == null && window.GetVisualDescendants().OfType<Button>().Any(b => b.Content?.ToString() == "Login"), "Navigation must return externally invalidated sessions to login");
        Check(!editModel.SignIn("sidebar-user", "incorrect") && editModel.CurrentRole == "" && editModel.CurrentUsername == null, "Failed account switch must not retain previous role");

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

    private static void CheckPendingOperationSession()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-pending-session-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        Check(!model.CanContinueWorkspaceOperation(model.SessionVersion), "Signed-out accounts must not start pending workspace operations");
        Check(model.SignIn("admin", "admin") && !model.CanContinueWorkspaceOperation(model.SessionVersion), "Required password changes must block pending operations");
        FormField[] change = [new("Current Password", "admin"), new("New Password", "operation-password"), new("Confirm", "operation-password")];
        Check(model.ChangeCurrentPassword(change), "Pending operation fixture must unlock");
        var version = model.SessionVersion;
        Check(model.CanContinueWorkspaceOperation(version), "Unchanged authenticated session must allow continuation");
        model.SignOut();
        Check(!model.CanContinueWorkspaceOperation(version), "Logout must invalidate a pending operation");
        Check(model.SignIn("admin", "operation-password"), "Same account must be able to sign in again");
        Check(!model.CanContinueWorkspaceOperation(version) && model.CanContinueWorkspaceOperation(model.SessionVersion), "Signing into the same account again must not revive an old operation");
        version = model.SessionVersion;
        using (var db = factory.CreateDbContext()) { db.Users.Single().PasswordChanged = false; db.SaveChanges(); }
        Check(!model.CanContinueWorkspaceOperation(version) && model.CurrentUsername == null, "External account changes must cancel pending operations");
    }

    private static void CheckCommittedJsonRestoreReloadFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-json-reload-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "JSON reload fixture", Price = 45, Quantity = 1 });
        Check(model.SaveInvoice() && model.SignIn("admin", "admin"), "JSON reload failure fixture must save and sign in");
        var backup = model.CreateJsonBackup();
        using (var db = factory.CreateDbContext()) { db.Invoices.Single().GrandTotal = 100m; db.SaveChanges(); }
        factory.SuccessfulCreationsRemaining = 1;
        Check(model.RestoreJsonBackup(backup), "Committed JSON restore must remain successful if reload fails");
        Check(model.Status.Contains("workspace could not reload", StringComparison.Ordinal) && model.Status.Contains("Restart LedgerNest", StringComparison.Ordinal), "JSON reload failure must show restart guidance");
        Check(model.CurrentUsername == null && !model.CanAccessWorkspace, "Partially reloaded JSON workspace must clear access");
        factory.SuccessfulCreationsRemaining = null;
        using (var db = factory.CreateDbContext()) Check(db.Invoices.Single().GrandTotal == 45m, "JSON replacement must already be committed before reload failure");
        Check(new MainWindowViewModel(factory, path).Invoices.Count == 1, "JSON restored records must be available after reopening");
    }

    private static void CheckCommittedRestoreReloadFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-restore-reload-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Committed restore", Price = 60, Quantity = 1 });
        Check(model.SaveInvoice() && model.SignIn("admin", "admin"), "Reload failure fixture must save and sign in");
        var backup = model.CreateDatabaseBackup();
        using (var db = factory.CreateDbContext()) { db.Invoices.Single().GrandTotal = 120m; db.SaveChanges(); }
        factory.FailCreation = true;
        Check(model.RestoreDatabaseBackup(backup), "A committed restore must report success even if workspace reload fails");
        Check(model.Status.Contains("workspace could not reload", StringComparison.Ordinal) && model.Status.Contains("Restart LedgerNest", StringComparison.Ordinal), "Post-commit reload failure must explain recovery without reporting a failed restore");
        Check(model.CurrentUsername == null && !model.CanAccessWorkspace, "Post-commit reload failure must retain the signed-out state");
        factory.FailCreation = false;
        using (var db = factory.CreateDbContext()) Check(db.Invoices.Single().GrandTotal == 60m, "Backup contents must already be committed despite the reload failure");
        Check(new MainWindowViewModel(factory, path).Invoices.Count == 1, "Restored data must be usable after reopening");
    }

    private static void CheckLockedDatabaseRestore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-locked-restore-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Lock fixture", Price = 90, Quantity = 1 });
        Check(model.SaveInvoice(), "Locked restore fixture must save");
        var backup = model.CreateDatabaseBackup();
        using (var db = factory.CreateDbContext())
        {
            db.Invoices.Single().GrandTotal = 180m;
            db.SaveChanges();
        }
        Check(model.SignIn("admin", "admin"), "Locked restore fixture must sign in");
        var stagingBefore = Directory.GetDirectories(Path.GetTempPath(), "ledgernest-restore-*").ToHashSet();
        using (var blocker = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            blocker.Open();
            using var transaction = blocker.BeginTransaction();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Check(!model.RestoreDatabaseBackup(backup), "Destination write lock must cause restore to fail safely");
            Console.WriteLine($"Locked restore returned after {timer.ElapsedMilliseconds} ms");
            Check(model.CurrentUsername == "admin", "Failed locked restore must not clear session identity");
            transaction.Rollback();
        }
        using (var db = factory.CreateDbContext())
            Check(db.Invoices.Single().GrandTotal == 180m && db.InvoiceItems.Count() == 1, "Failed copy must preserve current live data instead of applying the older backup");
        Check(!Directory.GetDirectories(Path.GetTempPath(), "ledgernest-restore-*").Except(stagingBefore).Any(), "Failed copy must clean its staging directory");
        Check(model.RestoreDatabaseBackup(backup), "Restore must succeed after the competing writer releases its lock");
        using (var db = factory.CreateDbContext()) Check(db.Invoices.Single().GrandTotal == 90m, "Successful retry must apply the backup contents");
        Check(model.CurrentUsername == null, "Successful restore retry must require fresh authentication");
    }

    private static void CheckJsonRestoreRollback()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-rollback-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Rollback original", Price = 90, Quantity = 1 });
        Check(model.SaveInvoice(), "Rollback fixture must save");
        var original = model.CreateJsonBackup();
        var replacement = JsonNode.Parse(original)!.AsObject();
        replacement["invoices"]![0]!["GrandTotal"] = 180;
        using (var db = factory.CreateDbContext())
            db.Database.ExecuteSqlRaw("CREATE TRIGGER reject_restore BEFORE INSERT ON invoice_items BEGIN SELECT RAISE(ABORT, 'Injected restore write failure'); END;");
        Check(!model.RestoreJsonBackup(replacement.ToJsonString()), "Restore must report an injected write failure");
        Check(model.CreateJsonBackup().Length > 0, "Database must remain readable after rollback");
        using (var db = factory.CreateDbContext())
        {
            Check(db.Invoices.Single().GrandTotal == 90m && db.InvoiceItems.Single().Description == "Rollback original", "Failure after deletion must roll back original invoice and item records");
            Check(db.Users.Single().Username == "admin" && db.Settings.Any(), "Rollback must retain accounts and settings");
            db.Database.ExecuteSqlRaw("DROP TRIGGER reject_restore");
        }
        Check(model.RestoreJsonBackup(original), "A failed restore must not leave locks that prevent retry");
        var reopened = new MainWindowViewModel(factory, path);
        Check(reopened.Invoices.Count == 1, "Rolled-back and retried records must survive reopening");
        factory.FailCreation = true;
        Check(!model.RestoreJsonBackup(original) && model.Status.StartsWith("Restore failed:", StringComparison.Ordinal), "Database setup failures must be reported instead of escaping the restore action");
        factory.FailCreation = false;
        Check(model.RestoreJsonBackup(original), "Restore must remain usable after a database setup failure");
    }

    private static void CheckRejectedJsonRestores()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-json-guard-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Protected JSON invoice", Price = 75, Quantity = 1 });
        Check(model.SaveInvoice(), "JSON restore fixture must persist");
        var valid = model.CreateJsonBackup();
        string Alter(Action<JsonObject> change)
        {
            var backup = JsonNode.Parse(valid)!.AsObject(); change(backup); return backup.ToJsonString();
        }
        var invalid = new[]
        {
            "{}", "[]", "null", "{", "{\"customers\":[],\"customers\":[]}", Alter(b => b.Remove("products")),
            Alter(b => b["customers"] = null), Alter(b => b["customers"] = new JsonObject()),
            Alter(b => b["customers"] = new JsonArray((JsonNode?)null)),
            Alter(b => b["_metadata"] = 42), Alter(b => b["_metadata"]!["version"] = 2),
            Alter(b => b["_metadata"]!["version"] = "9.0"),
            Alter(b => b["invoices"]![0]!["CustomerId"] = 999999),
            Alter(b => b["invoice_items"]![0]!["ProductId"] = 999999),
            Alter(b => b["invoice_items"]![0]!["InvoiceId"] = 999999),
            Alter(b => b["invoices"]!.AsArray().Add(b["invoices"]![0]!.DeepClone())),
            Alter(b => b["invoices"]![0]!["Id"] = 0)
        };
        foreach (var json in invalid)
        {
            Check(!model.RestoreJsonBackup(json), "Malformed or incomplete JSON backup must be rejected");
            using var db = factory.CreateDbContext();
            Check(db.Invoices.Single().GrandTotal == 75m && db.InvoiceItems.Single().Description == "Protected JSON invoice" && db.Users.Single().Username == "admin", "Rejected JSON restore must preserve invoice, item and account data");
        }
        Check(model.RestoreJsonBackup(valid), "Complete JSON exports must remain restorable");
        Check(model.RestoreJsonBackup(Alter(b => b.Remove("_metadata"))), "Complete older exports without metadata must remain supported");
        var empty = JsonNode.Parse(valid)!.AsObject();
        foreach (var table in new[] { "customers", "products", "company_info", "settings", "invoices", "invoice_items", "invoice_payments" }) empty[table] = new JsonArray();
        Check(model.RestoreJsonBackup(empty.ToJsonString()) && model.Invoices.Count == 0, "Explicit complete empty backups must remain distinguishable from missing tables");
    }

    private static void CheckRejectedDatabaseRestores()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-restore-guard-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Protected invoice", Price = 125, Quantity = 1 });
        Check(model.SaveInvoice() && model.SignIn("admin", "admin"), "Restore guard fixture must persist and sign in");
        var valid = model.CreateDatabaseBackup();
        byte[] AlterBackup(string sql)
        {
            var candidatePath = Path.Combine(Path.GetTempPath(), $"ledgernest-invalid-{Guid.NewGuid():N}.db");
            File.WriteAllBytes(candidatePath, valid);
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = candidatePath, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
            }
            var result = File.ReadAllBytes(candidatePath); File.Delete(candidatePath); return result;
        }
        var invalidBackups = new[]
        {
            Array.Empty<byte>(), System.Text.Encoding.UTF8.GetBytes("not a database"), valid[..100],
            AlterBackup("DROP TABLE users"),
            AlterBackup("UPDATE invoices SET Snapshot = 'invalid-json'"),
            AlterBackup("PRAGMA foreign_keys=OFF; UPDATE invoices SET CustomerId = 999999")
        };
        foreach (var invalid in invalidBackups)
        {
            Check(!model.RestoreDatabaseBackup(invalid), "Invalid backup must be rejected");
            using var db = factory.CreateDbContext();
            Check(db.Invoices.Single().GrandTotal == 125m && db.Users.Single().Username == "admin", "Rejected restore must preserve live invoices and credentials");
            Check(model.CurrentUsername == "admin" && model.ValidateSession(), "Rejected restore must preserve the current valid session");
        }
        Check(model.RestoreDatabaseBackup(valid), "Validated backup must still restore");
        Check(model.CurrentUsername == null && !model.CanAccessWorkspace, "Successful database restore must require a fresh login");
        Check(new MainWindowViewModel(factory, path).Invoices.Count == 1, "Restored records must survive reopening");
    }

    private static void CheckSessionInvalidation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-session-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        var fields = FormCatalog.User(); fields[0].Value = "session-user"; fields[1].Value = "session-password";
        Check(model.SaveRecord("User", fields), "Session invalidation fixture must save");
        foreach (var change in new[] { "role", "password", "salt", "required-change", "username", "delete" })
        {
            using (var db = factory.CreateDbContext())
            {
                var user = db.Users.Single(u => u.Username != "admin");
                user.Username = "session-user"; user.Role = "User"; user.PasswordChanged = true;
                user.Salt = PasswordCredentials.CreateSalt(); user.PasswordHash = PasswordCredentials.Hash("session-password", user.Salt);
                db.SaveChanges();
            }
            Check(model.SignIn("session-user", "session-password") && model.ValidateSession(), "Unchanged session must remain valid");
            using (var db = factory.CreateDbContext())
            {
                var user = db.Users.Single(u => u.Username == "session-user");
                switch (change)
                {
                    case "role": user.Role = "Admin"; break;
                    case "password": user.PasswordHash = PasswordCredentials.Hash("replacement-password", user.Salt); break;
                    case "salt": user.Salt = PasswordCredentials.CreateSalt(); break;
                    case "required-change": user.PasswordChanged = false; break;
                    case "username": user.Username = "renamed-user"; break;
                    case "delete": db.Users.Remove(user); break;
                }
                db.SaveChanges();
            }
            Check(!model.ValidateSession() && model.CurrentUsername == null && model.CurrentRole == "" && !model.CanAccessWorkspace, $"External {change} must invalidate session identity and access");
        }
        Check(!model.ValidateSession(), "Signed-out sessions must remain invalid");
    }

    private static void CheckPasswordMigration()
    {
        Check(PasswordCredentials.Hash("password", "0123456789ABCDEF") == "pbkdf2-sha256$v1$600000$D538B33181CB6504852E04C1DE79050BD42A58CF6DA21B9B127332677C5595BE", "PBKDF2 output must match an independently calculated reference vector");
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-password-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        const string password = " legacy password é ";
        var oldHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("old-salt" + password))).ToLowerInvariant();
        using (var db = factory.CreateDbContext())
        {
            var user = db.Users.Single();
            user.Salt = "old-salt"; user.PasswordHash = oldHash;
            db.SaveChanges();
        }
        Check(!model.VerifyUser("admin", "incorrect"), "Incorrect password must fail legacy verification");
        using (var db = factory.CreateDbContext()) Check(db.Users.Single().PasswordHash == oldHash, "Failed login must not migrate credentials");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        Check(model.SignIn("admin", password), "Existing C# password must authenticate and migrate");
        timer.Stop();
        Console.WriteLine($"Legacy credential upgrade: {timer.ElapsedMilliseconds} ms");
        string upgraded;
        using (var db = factory.CreateDbContext())
        {
            var user = db.Users.Single(); upgraded = user.PasswordHash;
            Check(upgraded.StartsWith("pbkdf2-sha256$v1$600000$", StringComparison.Ordinal) && user.Salt != "old-salt", "Successful verification must persist a versioned hash and fresh salt");
            Check(!user.PasswordChanged && user.Role == "Admin", "Hash migration must preserve role and mandatory password change");
            Check(PasswordCredentials.Verify(password, user.Salt, upgraded, out var needsUpgrade) && !needsUpgrade, "Modern hashes must verify without requesting migration");
            Check(!PasswordCredentials.Verify(password.Trim(), user.Salt, upgraded, out _), "Password whitespace must remain significant");
            foreach (var malformed in new[] { "", new string('z', 64), "pbkdf2-sha256$v2$600000$" + new string('0', 64), "pbkdf2-sha256$v1$999999999$" + new string('0', 64) })
                Check(!PasswordCredentials.Verify(password, user.Salt, malformed, out _), "Malformed and unsupported credential formats must fail closed");
        }
        var reloaded = new MainWindowViewModel(factory, path);
        Check(reloaded.SignIn("admin", password) && reloaded.RequiresPasswordChange, "Migrated credentials must survive restart and preserve forced change");
        using (var db = factory.CreateDbContext()) Check(db.Users.Single().PasswordHash == upgraded, "Modern login must not rewrite the hash");
    }

    private static void CheckAdministratorGuards()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-admin-guards-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        var admin = model.Users.Single();
        FormField[] Demote(string username) => [new("Username", username), new("Role", "User")];
        Check(!model.SaveRecord("User", Demote("admin"), admin), "Last admin demotion must fail");
        var spacedPassword = FormCatalog.User();
        spacedPassword[0].Value = " whitespace-user ";
        spacedPassword[1].Value = "  exact password  ";
        Check(model.SaveRecord("User", spacedPassword), "Password whitespace account must save");
        Check(model.VerifyUser("whitespace-user", "  exact password  ") && !model.VerifyUser("whitespace-user", "exact password"), "Creation must preserve password whitespace while normalizing username");
        var freshCredentials = new MainWindowViewModel(factory, path);
        Check(freshCredentials.VerifyUser("whitespace-user", "  exact password  "), "Exact password must authenticate after restart");
        Check(model.DeleteRecord("User", model.Users.Single(u => u.Name == "whitespace-user")), "Password fixture must be removable");
        var duplicate = FormCatalog.User();
        duplicate[0].Value = " ADMIN "; duplicate[1].Value = "duplicate-password";
        Check(!model.SaveRecord("User", duplicate) && model.Users.Count == 1, "Duplicate normalized usernames must fail without adding a row");
        var second = FormCatalog.User(); second[0].Value = "second-admin"; second[1].Value = "second-password"; second[2].Value = "Admin";
        Check(model.SaveRecord("User", second), "Second administrator must be creatable");
        var stale = new MainWindowViewModel(factory, path);
        Check(model.SaveRecord("User", Demote("admin"), admin), "Demotion must succeed when another administrator exists");
        Check(!stale.DeleteRecord("User", stale.Users.Single(u => u.Name == "second-admin")), "Stale view must use database administrator count before deletion");
        Check(!stale.SaveRecord("User", Demote("second-admin"), stale.Users.Single(u => u.Name == "second-admin")), "Stale view must use database administrator count before demotion");
        var refreshed = new MainWindowViewModel(factory, path);
        var rename = new[] { new FormField("Username", "second-admin"), new FormField("Role", "User") };
        Check(!refreshed.SaveRecord("User", rename, refreshed.Users.Single(u => u.Name == "admin")), "Renaming onto an existing username must fail");
        Check(refreshed.SignIn("admin", "admin"), "Ordinary user must be able to sign in");
        Check(refreshed.DeleteRecord("User", refreshed.Users.Single(u => u.Name == "admin")) && refreshed.CurrentUsername == null, "Deleting a non-admin account must clear its session");
        Check(!stale.SaveRecord("User", Demote("resurrected"), stale.Users.Single(u => u.Name == "admin")), "Stale edit must not recreate a deleted account");
        Check(new MainWindowViewModel(factory, path).Users.Single().Name == "second-admin", "All rejected mutations must preserve the remaining administrator");
    }

    private static void CheckInvoiceEditing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-edit-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.InvoiceCustomer[0].Value = "Original";
        model.InvoiceDetails[2].Value = "2026-12-31";
        model.InvoiceOptions[2].Value = "Original note";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Original item", Price = 100, Quantity = 2, TaxRate = 5 });
        Check(model.SaveInvoice(), "Editable invoice must save");
        var id = model.Invoices.Single().SourceId;
        var stale = new MainWindowViewModel(factory, path);
        Check(stale.LoadDocumentForEditing(stale.Invoices.Single()), "Second editor must load the original snapshot");
        var editor = new MainWindowViewModel(factory, path);
        Check(editor.LoadDocumentForEditing(editor.Invoices.Single()), "Persisted document must load for editing");
        Check(editor.InvoiceDetails[2].Value == "2026-12-31" && editor.InvoiceOptions[2].Value == "Original note" && editor.Totals.Total == 210, "Editor must restore saved inputs and totals");
        editor.Lines[0].Quantity = 3;
        editor.InvoiceOptions[2].Value = "Updated note";
        Check(editor.SaveInvoice(), "Edited document must save");
        Check(editor.Invoices.Count == 1 && editor.Invoices.Single().SourceId == id && editor.Invoices.Single().Name == "00000001", "Edit must preserve identity and number without adding a document");
        Check(editor.PeekNextDocumentNumber("Invoice") == "00000002", "Editing must not consume another number");
        editor.InvoiceCustomer[0].Value = "Updated customer";
        Check(editor.SaveInvoice(), "Repeated editing saves must remain updates");
        using (var db = factory.CreateDbContext())
            Check(db.Invoices.Single().GrandTotal == 315 && db.Invoices.Single().Snapshot!.Notes == "Updated note" && db.InvoiceItems.Count() == 1, "Edits must replace line data and snapshot atomically");
        stale.Lines[0].Price = 1;
        Check(!stale.SaveInvoice() && stale.Status.Contains("changed"), "Stale editor must not overwrite a newer saved version");
        var fresh = new MainWindowViewModel(factory, path);
        Check(fresh.LoadDocumentForEditing(fresh.Invoices.Single()), "Latest snapshot must reopen");
        var payment = FormCatalog.Payment(); payment[0].Value = "10";
        Check(model.ApplyPayment(model.Invoices.Single(), payment), "Payment during editing must apply");
        Check(!fresh.SaveInvoice(), "Payment arriving after editor load must prevent overwrite");
        var paid = new MainWindowViewModel(factory, path);
        Check(!paid.LoadDocumentForEditing(paid.Invoices.Single()), "Paid documents must not open in this edit workflow");
        using (var db = factory.CreateDbContext())
            Check(db.Invoices.Single().PaidAmount == 10 && db.Invoices.Single().GrandTotal == 315, "Rejected edit must preserve payment and invoice totals");
        editor.StartDocument("Quotation");
        Check(!editor.IsEditingDocument && editor.Lines.Count == 0, "Starting a new document must clear edit state");
    }

    private static void CheckInvoiceSnapshots()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-snapshots-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        string[] customer = ["Original Customer", "Original Business", "1234567890", "original@example.com", "GST-123", "Original Address"];
        for (var i = 0; i < customer.Length; i++) model.InvoiceCustomer[i].Value = customer[i];
        model.InvoiceDetails[2].Value = "2026-12-31";
        model.InvoiceDetails[3].Value = "Tax Invoice";
        model.InvoiceDetails[4].Value = "CUSTOM-17";
        model.HideInvoiceNumber.IsChecked = true;
        model.InterState.IsChecked = true;
        model.InvoiceOptions[0].Value = "Percentage";
        model.InvoiceOptions[1].Value = "10";
        model.InvoiceOptions[2].Value = "Historical notes";
        model.InvoiceOptions[3].Value = "Global";
        model.InvoiceOptions[4].Value = "7";
        model.AdditionalCosts.Add([new FormField("Description", "Delivery"), new FormField("Amount", "12.50", "number")]);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Snapshot item", Price = 100, Quantity = 2, Discount = 5, DiscountPerUnit = true });
        Check(model.SaveInvoice(), "Invoice with full editor snapshot must save");
        string ReadSnapshot()
        {
            using var db = factory.CreateDbContext();
            var invoice = db.Invoices.Single();
            var snapshot = invoice.Snapshot!;
            Check(snapshot != null && snapshot.Version == 1, "New invoices must carry versioned snapshots");
            Check(snapshot!.Customer == new LedgerNest.Domain.InvoiceCustomerSnapshot(customer[0], customer[1], customer[2], customer[3], customer[4], customer[5]), "All historical customer fields must persist");
            Check(snapshot.DueDate == new DateTime(2026, 12, 31) && snapshot.DocumentTitle == "Tax Invoice" && snapshot.CustomInvoiceNumber == "CUSTOM-17", "Dates and PDF title/number options must persist");
            Check(snapshot.HideInvoiceNumber && snapshot.IsInterState && snapshot.Notes == "Historical notes", "Invoice flags and notes must persist");
            Check(snapshot.TaxMode == "Global" && snapshot.TaxRate == 7 && snapshot.DiscountKind == "Percentage" && snapshot.DiscountValue == 10, "Calculation settings must persist");
            Check(snapshot.AdditionalCosts.Single() == new LedgerNest.Domain.InvoiceAdditionalCost("Delivery", 12.5m), "Additional charges must persist individually");
            var recalculated = InvoiceTotalsCalculator.Calculate(db.InvoiceItems.Select(item => new InvoiceLineInput(item.UnitPrice, item.Quantity, item.Discount, item.DiscountPerUnit, item.ExtraCost, item.TaxRate, item.PriceIncludesTax)).ToArray(), InvoiceTaxMode.Global, snapshot.TaxRate, snapshot.AdditionalCosts.Sum(c => c.Amount), InvoiceDiscountKind.Percent, snapshot.DiscountValue);
            Check(recalculated.Total == invoice.GrandTotal && recalculated.Tax == invoice.TaxTotal, "Persisted inputs must reproduce saved totals");
            return JsonSerializer.Serialize(snapshot);
        }
        var expected = ReadSnapshot();
        model.InvoiceCustomer[5].Value = "Changed Address";
        model.InvoiceOptions[2].Value = "Changed Notes";
        model.AdditionalCosts[0][1].Value = "999";
        Check(ReadSnapshot() == expected, "Changing current editor must not mutate historical snapshots");
        var backup = model.CreateJsonBackup();
        Check(model.RestoreJsonBackup(backup) && ReadSnapshot() == expected, "JSON restore must retain complete snapshots");
        var dbBackup = model.CreateDatabaseBackup();
        Check(model.RestoreDatabaseBackup(dbBackup) && ReadSnapshot() == expected, "Database restore must retain complete snapshots");
        using (var db = factory.CreateDbContext()) db.Database.ExecuteSqlRaw("ALTER TABLE invoices DROP COLUMN Snapshot");
        var upgraded = new MainWindowViewModel(factory, path);
        using (var db = factory.CreateDbContext())
            Check(db.Invoices.Single().Snapshot == null && upgraded.Invoices.Count == 1, "Pre-snapshot databases must preserve invoices without inventing missing history");
    }

    private static void CheckServiceTotals()
    {
        var service = new InvoiceService();
        var discounted = service.CalculateTotals([new LedgerNest.Domain.InvoiceItem { UnitPrice = 100, Quantity = 2, Discount = 10, DiscountPerUnit = true, ExtraCost = 5, TaxRate = 18 }]);
        Check(discounted.SubTotal == 185m && discounted.DiscountTotal == 20m && discounted.TaxTotal == 33.3m && discounted.GrandTotal == 218.3m, "Application service must honor per-unit discounts and extra costs");
        var inclusive = service.CalculateTotals([new LedgerNest.Domain.InvoiceItem { UnitPrice = 118, Quantity = 2, PriceIncludesTax = true, TaxRate = 18 }]);
        Check(inclusive.SubTotal == 200m && inclusive.TaxTotal == 36m && inclusive.GrandTotal == 236m, "Application service must back tax out of inclusive prices");
        var fractional = service.CalculateTotals(Enumerable.Range(0, 3).Select(_ => new LedgerNest.Domain.InvoiceItem { UnitPrice = .03m, Quantity = 1, TaxRate = 18 }));
        Check(fractional.TaxTotal == .0162m && fractional.GrandTotal == .1062m, "Application service must not round tax per line before aggregation");
        var empty = service.CalculateTotals([]);
        Check(empty == new InvoiceTotals(0, 0, 0, 0), "Empty invoice totals must be zero");
    }

    private static void CheckFormRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-forms-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        var customer = FormCatalog.Customer();
        customer[0].Value = "Business owner";
        customer[1].Value = "Roundtrip business";
        customer[2].Value = "1234567890";
        Check(model.SaveRecord("Customer", customer), "Business customer must save");
        var product = FormCatalog.Product();
        foreach (var field in product)
            field.Value = field.Kind switch
            {
                "toggle" => "true",
                "number" => "12.5",
                "date" => "2027-06-15",
                "choice" => field.Label == "Type" ? "Service" : "Custom…",
                _ => "Saved " + field.Label
            };
        foreach (var field in product.Where(f => f.Kind == "toggle")) field.IsChecked = true;
        Check(model.SaveRecord("Product", product), "Full product form must save");
        void Verify(MainWindowViewModel loaded)
        {
            Check(loaded.Customers.Single()["Business Name"] == "Roundtrip business", "Business name must survive persistence");
            var record = loaded.Products.Single();
            foreach (var field in product)
                Check(field.Kind == "toggle"
                    ? bool.Parse(record[field.Label]) == field.IsChecked
                    : record[field.Label] == field.Value,
                    $"Product field {field.Label} must round trip");
        }
        Verify(new MainWindowViewModel(factory, path));
        var backup = model.CreateJsonBackup();
        Check(model.RestoreJsonBackup(backup), "Full forms must restore from JSON");
        Verify(new MainWindowViewModel(factory, path));
        var edited = new MainWindowViewModel(factory, path);
        edited.AddProductLine(edited.Products.Single());
        var addedLine = edited.Lines.Single();
        addedLine.Quantity = 3;
        Check(addedLine.DiscountPerUnit && addedLine.PriceIncludesTax && addedLine.Discount == 12.5m && addedLine.Total == 0, "Product selection must apply the legacy per-unit default discount and tax-inclusive flag");
        product.Single(f => f.Label == "Type").Value = "Product";
        product.Single(f => f.Label == "Alias Name (for invoice PDF)").Value = "Edited alias";
        Check(edited.SaveRecord("Product", product, edited.Products.Single()), "Full product edit must save");
        Verify(new MainWindowViewModel(factory, path));
        using (var db = factory.CreateDbContext())
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE customers DROP COLUMN BusinessName");
            db.Database.ExecuteSqlRaw("ALTER TABLE products DROP COLUMN AliasName");
            db.Database.ExecuteSqlRaw("ALTER TABLE products DROP COLUMN PriceIncludesTax");
        }
        var upgraded = new MainWindowViewModel(factory, path);
        Check(upgraded.Customers.Count == 1 && upgraded.Customers.Single()["Business Name"] == "", "Existing customer schema must upgrade preserving records");
        Check(upgraded.Products.Single()["Alias Name (for invoice PDF)"] == "" && !bool.Parse(upgraded.Products.Single()["Price includes tax"]), "Existing product schema must upgrade with safe defaults");
        var setup = model.CreateOnboardingFields();
        setup[0].Single(f => f.Label == "Company Name").Value = "Setup company";
        setup[0].Single(f => f.Label == "Country").Value = "Nepal";
        setup[1].Single(f => f.Label == "Starting Number").Value = "27";
        setup[1].Single(f => f.Label == "Leading Zeros").IsChecked = false;
        setup[1].Single(f => f.Label == "Default Tax Rate (%)").Value = "13";
        setup[2].Single(f => f.Label == "Page Size").Value = "A5";
        setup[2].Single(f => f.Label == "Template").Value = "Modern";
        Check(model.CompleteOnboarding(setup), "First-time setup must save");
        var savedSetup = new MainWindowViewModel(factory, path).CreateOnboardingFields();
        Check(savedSetup[0][0].Value == "Setup company" && savedSetup[0][1].Value == "Nepal", "Setup company and country must reload");
        Check(savedSetup[1][2].Value == "27" && !savedSetup[1][3].IsChecked && savedSetup[1][4].Value == "13", "Setup invoice preferences must reload");
        Check(savedSetup[2][0].Value == "A5" && savedSetup[2][1].Value == "Modern", "Setup appearance must reload");
        using (var db = factory.CreateDbContext())
            Check(db.CompanyInfos.Single().Name == "Setup company" && db.Settings.Single(s => s.Key == "onboarding.completed").Value == "true", "Setup must persist company entity and completion together");
        setup[0][0].Value = "Must not save";
        setup[1][2].Value = "1.5";
        Check(!model.CompleteOnboarding(setup), "Setup must reject fractional starting number");
        Check(new MainWindowViewModel(factory, path).CreateOnboardingFields()[0][0].Value == "Setup company", "Invalid setup must leave persisted values untouched");
        var user = FormCatalog.User();
        user[0].Value = "second-user"; user[1].Value = "initial-password";
        Check(model.SaveRecord("User", user), "Second user must save");
        Check(model.SignIn("second-user", "initial-password") && model.CurrentUsername == "second-user", "Login must select the actual user");
        FormField[] passwords = [new("Current Password", "initial-password"), new("New Password", "replacement-password"), new("Confirm", "replacement-password")];
        Check(model.ChangeCurrentPassword(passwords), "Password change must target signed-in user");
        Check(model.VerifyUser("second-user", "replacement-password") && model.VerifyUser("admin", "admin"), "Password change must leave other users unchanged");
        Check(!model.SignIn("second-user", "wrong") && model.CurrentUsername == null, "Failed login must clear session");
        Check(!model.ChangeCurrentPassword(passwords), "Password change must require a signed-in user");
        Check(model.SignIn("admin", "admin") && model.RequiresPasswordChange, "Default admin login must request password change");
        model.Lines.Add(new InvoiceLineViewModel { Name = "Stale draft", Price = 1 });
        model.InvoiceCustomer[0].Value = "Stale customer";
        model.StartDocument("Quotation");
        Check(model.Title == "New Invoice" && model.InvoiceDetails[0].Value == "Quotation" && model.Lines.Count == 0 && model.InvoiceCustomer[0].Value == "", "New quotation must open a fresh quotation editor");
        model.StartDocument("Receipt");
        Check(model.InvoiceDetails[0].Value == "Receipt", "New receipt must select receipt type");
    }

    private static void CheckReceivablesLifecycle()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-receivables-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.InvoiceCustomer[0].Value = "Receivable customer";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100, Quantity = 2, TaxRate = 5 });
        model.InvoiceOptions[0].Value = "Amount";
        model.InvoiceOptions[1].Value = "10";
        Check(model.SaveInvoice(), "Receivables invoice must save");
        void Verify(MainWindowViewModel loaded, string paid, string outstanding, string status, decimal balance)
        {
            var invoice = loaded.Invoices.Single();
            Check(invoice["Paid"] == paid && invoice["Outstanding"] == outstanding && invoice["Status"] == status, "Invoice balance fields must match payment state");
            Check(loaded.BuildReport("Revenue").Outstanding == balance, "Revenue summary must reflect outstanding immediately");
            var report = loaded.BuildReport("Receivables");
            Check(balance > 0 ? report.Rows.Length == 2 && report.Rows[1][3] == $"₹ {balance:0.00}" : report.Rows.Length == 1, "Receivables must include only unpaid balances");
        }
        Verify(model, "0.00", "200.00", "Unpaid", 200);
        Verify(new MainWindowViewModel(factory, path), "0.00", "200.00", "Unpaid", 200);
        var payment = FormCatalog.Payment(); payment[0].Value = "50";
        Check(model.ApplyPayment(model.Invoices.Single(), payment), "Partial payment must apply");
        Verify(model, "50.00", "150.00", "Partial", 150);
        Verify(new MainWindowViewModel(factory, path), "50.00", "150.00", "Partial", 150);
        payment[0].Value = "150";
        Check(model.ApplyPayment(model.Invoices.Single(), payment), "Final payment must apply");
        Verify(model, "200.00", "0.00", "Paid", 0);
        Verify(new MainWindowViewModel(factory, path), "200.00", "0.00", "Paid", 0);
    }

    private static void CheckTaxReport()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-tax-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.InvoiceDetails[1].Value = "2026-01-15";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Five percent", Price = 100, TaxRate = 5 });
        model.Lines.Add(new InvoiceLineViewModel { Name = "Eighteen percent", Price = 100, TaxRate = 18 });
        model.InvoiceOptions[0].Value = "Amount";
        model.InvoiceOptions[1].Value = "20";
        Check(model.SaveInvoice(), "Mixed-rate discounted invoice must save");
        var mixed = model.Invoices.Last();
        model.Lines.Clear();
        model.Lines.Add(new InvoiceLineViewModel { Name = "Inclusive", Price = 118, TaxRate = 18, PriceIncludesTax = true });
        model.InvoiceOptions[0].Value = "None";
        Check(model.SaveInvoice(), "Tax-inclusive invoice must save");
        model.Lines.Clear();
        model.Lines.Add(new InvoiceLineViewModel { Name = "Global", Price = 100, TaxRate = 18 });
        model.InvoiceOptions[3].Value = "Global";
        model.InvoiceOptions[4].Value = "7";
        Check(model.SaveInvoice(), "Global-rate invoice must save");
        model.InvoiceOptions[3].Value = "No Tax";
        Check(model.SaveInvoice(), "Tax-free invoice must save");
        model.InvoiceOptions[3].Value = "Per Item";
        model.InvoiceDetails[0].Value = "Quotation";
        Check(model.SaveInvoice(), "Quotation must save without entering invoice tax report");
        void Verify(MainWindowViewModel loaded, string amount)
        {
            var report = loaded.BuildReport("Tax");
            Check(report.Rows.Length == 2 && report.Rows[0][1] == "Tax" && report.Rows[1][1] == amount, "Tax report must sum actual invoice tax rather than infer an 18 percent rate");
            Check(loaded.ExportReportCsv("Tax").Contains(amount), "Tax CSV must use the same actual tax totals");
        }
        Verify(model, "₹ 48.00");
        Verify(new MainWindowViewModel(factory, path), "₹ 48.00");
        var backup = model.CreateJsonBackup();
        Check(model.RestoreJsonBackup(backup), "Tax report backup must restore");
        Verify(model, "₹ 48.00");
        Check(model.SetDocumentTrash(model.Invoices.Single(i => i.SourceId == mixed.SourceId), true), "Tax test invoice must move to trash");
        Verify(new MainWindowViewModel(factory, path), "₹ 25.00");
    }

    private static void CheckRecordDeletion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-delete-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        var customer = FormCatalog.Customer();
        customer[0].Value = "Historical customer"; customer[2].Value = "1234567890";
        Check(model.SaveRecord("Customer", customer), "Deletion test customer must save");
        var product = FormCatalog.Product();
        product[1].Value = "Historical product"; product[5].Value = "100";
        Check(model.SaveRecord("Product", product), "Deletion test product must save");
        model.InvoiceCustomer[0].Value = customer[0].Value;
        model.AddProductLine(model.Products.Single());
        Check(model.SaveInvoice(), "Deletion test invoice must save");
        using (var db = factory.CreateDbContext())
        {
            // Exercise a pre-snapshot invoice linked to both catalog records.
            db.Invoices.Single().CustomerName = "";
            db.InvoiceItems.Single().ProductId = model.Products.Single().SourceId;
            db.SaveChanges();
        }
        Check(model.DeleteRecord("Customer", model.Customers.Single()), "Customer deletion must persist");
        Check(model.DeleteRecord("Product", model.Products.Single()), "Product deletion must persist");
        var reloaded = new MainWindowViewModel(factory, path);
        Check(!reloaded.Customers.Any() && !reloaded.Products.Any(), "Deleted catalog records must stay deleted after restart");
        Check(reloaded.Invoices.Single()["Customer"] == "Historical customer", "Deleting a customer must retain invoice customer identity");
        Check(reloaded.BuildReport("Products").Rows.Any(r => r[0] == "Historical product" && r[1] == "1"), "Deleting a product must retain historical sales");
        using (var db = factory.CreateDbContext())
            Check(db.Invoices.Single().CustomerId == null && db.InvoiceItems.Single().ProductId == null, "Deletion must detach obsolete catalog references");
        var backup = reloaded.CreateJsonBackup();
        Check(reloaded.RestoreJsonBackup(backup) && reloaded.Invoices.Single()["Customer"] == "Historical customer", "Backup must preserve history after catalog deletion");
        var user = FormCatalog.User();
        user[0].Value = "deletable"; user[1].Value = "test-password";
        Check(reloaded.SaveRecord("User", user) && reloaded.SignIn("deletable", "test-password"), "Deletion test user must sign in");
        Check(reloaded.DeleteRecord("User", reloaded.Users.Single(u => u.Name == "deletable")) && reloaded.CurrentUsername == null, "Deleting signed-in user must clear session");
        Check(!new MainWindowViewModel(factory, path).VerifyUser("deletable", "test-password"), "Deleted user must not authenticate after restart");
        Check(!reloaded.DeleteRecord("User", reloaded.Users.Single()), "Last administrator deletion must be rejected");
        Check(new MainWindowViewModel(factory, path).Users.Single().Name == "admin", "Last administrator must remain available after restart");
    }

    private static void CheckDocumentTrash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-trash-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Trash item", Price = 100, Quantity = 1 });
        Check(model.SaveInvoice(), "Trash test invoice must save");
        using (var db = factory.CreateDbContext()) db.Database.ExecuteSqlRaw("ALTER TABLE invoices DROP COLUMN DeletedAt");
        model = new MainWindowViewModel(factory, path);
        Check(model.ActiveInvoices.Count() == 1, "Older C# databases must gain trash state without losing invoices");
        var record = model.Invoices.Single();
        var payment = FormCatalog.Payment();
        payment[0].Value = "20";
        Check(model.ApplyPayment(record, payment), "Trash test payment must save");
        Check(!model.DeleteDocumentPermanently(record), "Active document must not be permanently deleted");
        Check(model.SetDocumentTrash(record, true), "Document must move to trash");
        Check(!model.ActiveInvoices.Any() && model.BuildReport("Revenue").Billed == 0, "Trash must be excluded from dashboard and revenue");
        Check(!model.ExportCsv("Invoice").Contains("00000001"), "Default document export must exclude trash");
        Check(model.PeekNextDocumentNumber("Invoice") == "00000002", "Trashed document number must remain reserved");
        var reloaded = new MainWindowViewModel(factory, path);
        record = reloaded.Invoices.Single();
        Check(reloaded.DeletedRecords.Contains(record.Id), "Trash must survive restart");
        Check(reloaded.BuildReport("Products").Rows.Length == 1, "Trashed invoice items must not count as product sales");
        Check(!reloaded.ApplyPayment(record, payment), "Trashed invoices must reject new payments");
        var backup = reloaded.CreateJsonBackup();
        Check(reloaded.RestoreJsonBackup(backup) && reloaded.DeletedRecords.Contains(reloaded.Invoices.Single().Id), "JSON backup must preserve trash state");
        var dbBackup = reloaded.CreateDatabaseBackup();
        Check(reloaded.SetDocumentTrash(reloaded.Invoices.Single(), false), "Document must restore from trash");
        Check(reloaded.RestoreDatabaseBackup(dbBackup) && reloaded.DeletedRecords.Contains(reloaded.Invoices.Single().Id), "Database backup must preserve trash state");
        Check(reloaded.SetDocumentTrash(reloaded.Invoices.Single(), false), "Restored backup document must restore from trash");
        var restored = new MainWindowViewModel(factory, path);
        record = restored.Invoices.Single();
        Check(restored.ActiveInvoices.Count() == 1 && restored.BuildReport("Revenue").Billed == 100 && restored.Payments.Count == 1, "Restore must recover report totals and payment history after restart");
        Check(restored.SetDocumentTrash(record, true) && restored.DeleteDocumentPermanently(record), "Trashed document must support permanent deletion");
        using (var db = factory.CreateDbContext())
            Check(!db.Invoices.Any() && !db.InvoiceItems.Any() && !db.Payments.Any(), "Permanent deletion must remove document, items and payments");
        Check(!restored.Invoices.Any() && !restored.Payments.Any() && !restored.DeletedRecords.Any(), "Permanent deletion must update in-memory collections");
    }

    private static void CheckDocumentNumbering()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-numbering-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Settings["Invoice Settings"].SelectMany(s => s.Fields).Single(f => f.Label == "Starting Number").Value = "27";
        Check(model.SaveSettings("Invoice Settings"), "Starting number must persist");
        Check(model.PeekNextDocumentNumber("Invoice") == "00000027" && model.PeekNextDocumentNumber("Invoice") == "00000027", "Number preview must honor settings without consuming a number");
        Check(model.PeekNextDocumentNumber("Quotation") == "00000001" && model.PeekNextDocumentNumber("Receipt") == "00000001", "Quotation and receipt sequences must start independently at one");
        var stale = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Numbered item", Price = 1 });
        stale.Lines.Add(new InvoiceLineViewModel { Name = "Numbered item", Price = 1 });
        Check(model.SaveInvoice() && stale.SaveInvoice(), "Two already-open editors must save distinct numbers");
        using (var db = factory.CreateDbContext())
            Check(db.Invoices.Select(i => i.InvoiceNumber).ToArray().Order().SequenceEqual(new[] { "00000027", "00000028" }), "Save must derive its number from current database rows");
        model.InvoiceDetails[0].Value = "Quotation";
        Check(model.SaveInvoice() && model.Invoices.Last().Name == "00000001", "Quotation must use its own sequence");
        Check(new MainWindowViewModel(factory, path).PeekNextDocumentNumber("Invoice") == "00000029", "Invoice sequence must continue across restart");
        var backup = model.CreateJsonBackup();
        Check(model.RestoreJsonBackup(backup) && model.PeekNextDocumentNumber("Quotation") == "00000002", "Number sequence must survive backup restore");
        using (var db = factory.CreateDbContext())
        {
            db.Invoices.Add(new LedgerNest.Domain.Invoice { InvoiceNumber = "INV-0099", Type = "Receipt" });
            db.SaveChanges();
        }
        Check(model.PeekNextDocumentNumber("Receipt") == "00000100", "Pre-existing C# display numbers must retain numbering continuity");
    }

    private static void CheckDocumentTypes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-types-{Guid.NewGuid():N}.db");
        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options);
        var model = new MainWindowViewModel(factory, path);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Type test", Price = 10, Quantity = 1 });
        foreach (var type in new[] { "Invoice", "Quotation", "Receipt" })
        {
            model.InvoiceDetails[0].Value = type;
            Check(model.SaveInvoice(), $"{type} must save");
        }
        void Verify(MainWindowViewModel loaded)
        {
            foreach (var type in new[] { "Invoice", "Quotation", "Receipt" })
                Check(loaded.Invoices.Count(i => i["Type"] == type) == 1, $"{type} must retain its type");
            Check(loaded.BuildReport("Products").Rows.Any(r => r[0] == "Type test" && r[1] == "1"), "Product sales must exclude quotations and receipts");
        }
        Verify(new MainWindowViewModel(factory, path));
        var backup = model.CreateJsonBackup();
        Check(model.RestoreJsonBackup(backup), "Document backup must restore");
        Verify(new MainWindowViewModel(factory, path));
        using (var db = factory.CreateDbContext())
            db.Database.ExecuteSqlRaw("ALTER TABLE invoices DROP COLUMN Type");
        var upgraded = new MainWindowViewModel(factory, path);
        Check(upgraded.Invoices.Count == 3 && upgraded.Invoices.All(i => i["Type"] == "Invoice"), "Pre-type C# databases must upgrade without losing invoices");
        Check(new MainWindowViewModel(factory, path).Invoices.Count == 3, "Schema upgrade must be repeatable");
    }

    private static void CheckPersistence()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ledgernest-ui-checks", Guid.NewGuid().ToString("N"), "ledgernest.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        var factory = new TestDbContextFactory(options);

        var model = new MainWindowViewModel(factory, dbPath);
        var customer = FormCatalog.Customer();
        customer[0].Value = "Persisted Customer";
        customer[2].Value = "5551234567";
        Check(model.SaveRecord("Customer", customer), "Customer must save to SQLite");
        var product = FormCatalog.Product();
        product[1].Value = "Persisted Product";
        product[5].Value = "42.50";
        product[6].Value = "42.50";
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
        model.Lines.Add(new InvoiceLineViewModel { Name = "Persisted Product", Price = 42.50m, Quantity = 2, TaxRate = 18, Discount = 1.25m, DiscountPerUnit = true });
        Check(model.SaveInvoice(), "Invoice must save to SQLite");
        var payment = FormCatalog.Payment();
        payment[0].Value = "40";
        payment[2].Value = "UPI";
        payment[4].Value = "txn-123";
        Check(model.ApplyPayment(model.Invoices.Single(), payment), "Payment must save to SQLite");
        Check(model.Invoices.Single()["Status"] == "Partial", "Partial payment must update invoice status");
        Check(model.Invoices.Single()["Outstanding"] == "57.35", "Partial payment must update outstanding balance");

        var reloaded = new MainWindowViewModel(factory, dbPath);
        Check(reloaded.Customers.Any(c => c.Name == "Persisted Customer"), "Customers must reload from SQLite");
        Check(reloaded.Customers.Any(c => c.Name == "Comma, Customer" && c["Address"] == "Street 1, City"), "Imported customers must reload from SQLite");
        Check(reloaded.Products.Any(p => p.Name == "Persisted Product" && p["Sale Price"] == "42.5"), "Products must reload from SQLite");
        Check(reloaded.Products.Any(p => p.Name == "Imported Widget" && p["Sale Price"] == "12.75" && p["HSN/SAC"] == "HSN-55"), "Imported products must reload from SQLite");
        Check(reloaded.Invoices.Any(i => i["Customer"] == "Persisted Customer" && i["Total"] == "97.35" && i["Status"] == "Partial"), "Invoices must reload from SQLite");
        Check(reloaded.BuildReport("Products").Rows.Any(r => r[0] == "Persisted Product" && r[1] == "2" && r[2] == "₹ 85.00" && r[4] == "₹ -2.50" && r[5] == "-2.9%"), "Product report must reload historical invoice item cost and discount data from SQLite");
        Check(reloaded.Payments.Any(p => p.Name == "00000001-R1" && p["Amount"] == "40.00" && p["Method"] == "UPI"), "Payments must reload from SQLite");
        var persistedUser = FormCatalog.User();
        persistedUser[0].Value = "persisted-admin";
        persistedUser[1].Value = "temporary-secret";
        persistedUser[2].Value = "Admin";
        Check(model.SaveRecord("User", persistedUser), "User must save to SQLite");
        reloaded = new MainWindowViewModel(factory, dbPath);
        Check(reloaded.Users.Any(u => u.Name == "admin" && u["Role"] == "Admin") && reloaded.VerifyUser("admin", "admin"), "Default admin must be available for first login");
        Check(reloaded.Users.Any(u => u.Name == "persisted-admin" && u["Role"] == "Admin" && !u.Values.ContainsKey("Password")), "Users must reload from SQLite without exposing passwords");
        Check(reloaded.VerifyUser("persisted-admin", "temporary-secret") && !reloaded.VerifyUser("persisted-admin", "wrong-secret"), "Persisted users must authenticate with their salted password hash");
        var passwordChange = new[] { new FormField("Current Password", "temporary-secret", "password", required: true), new FormField("New Password", "changed-secret", "password", required: true), new FormField("Confirm New Password", "changed-secret", "password", required: true) };
        Check(reloaded.ChangePassword("persisted-admin", passwordChange) && reloaded.VerifyUser("persisted-admin", "changed-secret"), "Users must be able to change passwords");
        using (var userDb = factory.CreateDbContext())
        {
            var savedUser = userDb.Users.Single(u => u.Username == "persisted-admin");
            Check(savedUser.Salt.Length > 0 && savedUser.PasswordHash.StartsWith("pbkdf2-sha256$v1$600000$", StringComparison.Ordinal) && savedUser.PasswordHash != "temporary-secret", "User password must be persisted only as a salted hash");
        }
        var pdfBytes = reloaded.ExportDocumentPdf(reloaded.Invoices.Single(i => i.Name == "00000001"));
        Check(pdfBytes.Length > 500 && System.Text.Encoding.ASCII.GetString(pdfBytes.Take(8).ToArray()).StartsWith("%PDF-1."), "Invoice PDF export must create a valid PDF document");

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

        var dbBackup = model.CreateDatabaseBackup();
        Check(dbBackup.Length > 1024, "Database-file backup must include SQLite bytes");
        var afterDbBackupCustomer = FormCatalog.Customer();
        afterDbBackupCustomer[0].Value = "After DB Backup";
        afterDbBackupCustomer[2].Value = "6666666666";
        Check(model.SaveRecord("Customer", afterDbBackupCustomer), "Customer after DB backup must save");
        Check(model.RestoreDatabaseBackup(dbBackup), "Database-file backup must restore successfully");
        Check(model.Customers.Any(c => c.Name == "Persisted Customer") && !model.Customers.Any(c => c.Name == "After DB Backup"), "Database-file restore must replace current data");

        var backupJson = model.CreateJsonBackup();
        var backup = JsonNode.Parse(backupJson)!.AsObject();
        Check(backup["_metadata"]?["app_name"]?.GetValue<string>() == Branding.Name, "Backup metadata must use current branding");
        Check(backup["customers"]?.AsArray().Count >= 2 && backup["invoice_payments"]?.AsArray().Count == 1, "Backup must include migrated data tables");
        Check(!backup.ContainsKey("users"), "Backup JSON must exclude users like the legacy app");
        var extraCustomer = FormCatalog.Customer();
        extraCustomer[0].Value = "Temporary After Backup";
        extraCustomer[2].Value = "7777777777";
        Check(model.SaveRecord("Customer", extraCustomer), "Temporary customer must save before restore");
        Check(model.Customers.Any(c => c.Name == "Temporary After Backup"), "Temporary customer must appear before restore");
        Check(model.RestoreJsonBackup(backupJson), "JSON backup must restore successfully");
        Check(model.Customers.Any(c => c.Name == "Persisted Customer") && !model.Customers.Any(c => c.Name == "Temporary After Backup"), "Restore must replace current customer data with backup data");
        Check(model.Invoices.Any(i => i["Customer"] == "Persisted Customer" && i["Status"] == "Partial"), "Restore must bring invoices and payment status back");
        Check(model.BuildReport("Receivables").Outstanding == 57.35m, "Receivables report must use restored outstanding balance");
        Check(model.ExportReportCsv("Customers").Contains("Persisted Customer"), "Customer report CSV must include restored customer totals");
        var reportPdf = model.ExportReportPdf("Customers");
        Check(reportPdf.Length > 500 && System.Text.Encoding.ASCII.GetString(reportPdf.Take(8).ToArray()).StartsWith("%PDF-1."), "Report PDF export must create a valid PDF document");

        reloaded = new MainWindowViewModel(factory, dbPath);
        model.SetThemeMode("Dark");
        model.SetLanguage("हिन्दी");
        reloaded = new MainWindowViewModel(factory, dbPath);
        Check(reloaded.ThemeMode == "Dark", "Theme mode must persist and reload from SQLite");
        Check(reloaded.Language == "हिन्दी", "Language preference must persist and reload from SQLite");
        var reloadedCompanyFields = reloaded.Settings["Company Info"][1].Fields.ToDictionary(f => f.Label);
        Check(reloadedCompanyFields["Company Name"].Value == "LedgerNest Labs" && reloadedCompanyFields["GSTIN"].Value == "GST-123", "Company info must reload from SQLite");
        var reloadedInvoiceGeneral = reloaded.Settings["Invoice Settings"].Single(s => s.Title == "General").Fields.ToDictionary(f => f.Label);
        Check(reloadedInvoiceGeneral["Invoice Prefix"].Value == "LN-" && reloadedInvoiceGeneral["Starting Number"].Value == "27", "Invoice settings must reload from SQLite");
        Check(reloaded.Settings["PDF Settings"][1].Fields[0].Value == "Grid Classic" && reloaded.Settings["PDF Settings"][3].Fields[0].Value == "#0F766E", "PDF settings must reload from SQLite");
    }

    private sealed class TestDbContextFactory(DbContextOptions<LedgerNestDbContext> options) : IDbContextFactory<LedgerNestDbContext>
    {
        public bool FailCreation { get; set; }
        public int? SuccessfulCreationsRemaining { get; set; }
        public LedgerNestDbContext CreateDbContext()
        {
            if (FailCreation || SuccessfulCreationsRemaining == 0) throw new InvalidOperationException("Injected database setup failure");
            if (SuccessfulCreationsRemaining.HasValue) SuccessfulCreationsRemaining--;
            return new(options);
        }
    }
}
