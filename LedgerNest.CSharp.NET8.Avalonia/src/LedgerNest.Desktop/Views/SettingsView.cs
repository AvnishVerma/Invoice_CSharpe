using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;
using Avalonia.Platform.Storage;
using System.Text;
using System.Collections.ObjectModel;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private string settingsTab = "Company Info";
    private readonly ObservableCollection<BackupHistoryItem> backupHistory = [];
    // Performs the settings view action by preparing settings navigation data and loading the XAML settings view.
    private Control SettingsView()
    {
        SettingsTabModel[] tabs =
        [
            new("Company Info", "business"), new("Backup", "backup"), new("Users", "people"),
            new("PDF Settings", "settings"), new("Invoice Settings", "receipt_long"), new("Product Details", "view_column"),
            new("Customize", "tune"), new("Accessibility", "accessibility_new"), new("Software Info", "info_outline")
        ];
        return new SettingsPageView(new SettingsPageModel(tabs, settingsTab, SettingsContent, selected => settingsTab = selected));
    }

    // Performs the settings content selection action for this screen or workflow.
    private Control SettingsContent(string name) => name switch
    {
        "Company Info" => CompanySettingsView(),
        "PDF Settings" => PdfSettingsView(),
        "Users" => new ManagementView(Model, "User", this),
        "Backup" => BackupView(),
        "Customize" => CustomizationView(),
        "Software Info" => SoftwareInfo(),
        _ => SettingsForm(name)
    };
    // Performs the settings form action for this screen or workflow.
    private Control SettingsForm(string name)
    {
        if (name == "Invoice Settings") return InvoiceSettingsView();
        if (!Model.Settings.TryGetValue(name, out var sections)) return Ui.Empty("No settings available");
        var stack = Ui.Stack(16);
        foreach (var section in sections) stack.Children.Add(Ui.Card(Ui.Stack(16, Ui.Text(section.Title, 18, true), Ui.Fields(section.Fields)), 24));
        if (name == "Accessibility") stack.Children.Add(Ui.Card(Ui.Stack(16, Ui.Text("Keyboard Shortcuts", 20, true), Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + O", "Preview PDF"), Shortcut("Ctrl + P", "Print invoice"))));
        stack.Children.Add(Ui.Button("Save Settings", () => Model.SaveSettings(name), true));
        stack.MaxWidth = 900; return Ui.Rows("Auto,*", Ui.AppBar(name), Ui.Scroll(stack, 28));
    }
    // Performs the invoice settings view action for this screen or workflow.
    private Control InvoiceSettingsView()
    {
        var sections = Model.Settings["Invoice Settings"];
        string[] order = ["General", "Branding", "Tax", "Items", "Customer", "Columns", "Custom Fields"];
        string[] labels = ["General", "Branding", "Tax & GST", "Invoice Items", "Customer Details", "Invoice Columns", "Custom Fields"];
        var content = new ContentControl(); var buttons = new List<Button>(); var nav = Ui.Stack(4);
        void Select(int index)
        {
            foreach (var button in buttons) button.Classes.Set("selected", (int?)button.Tag == index);
            var section = sections.First(s => s.Title == order[index]);
            Control fields;
            if (index == 0)
            {
                FormField F(string label) => section.Fields.First(f => f.Label == label);
                fields = Ui.Stack(16, Ui.Fields([F("Invoice Prefix"), F("Starting Number")], 2), Ui.Fields([F("Leading Zeros"), F("Currency")], 2), Ui.Fields([F("Date Format"), F("Time Format")], 2), Ui.Fields([F("Show time in PDF"), F("Quantity Column")], 2), Ui.Field(F("Additional Information")), Ui.Field(F("Thank You Note")), Ui.Field(F("Hide Invoice Number")));
            }
            else fields = Ui.Fields(section.Fields);
            var card = Ui.Card(Ui.Stack(32, Ui.Columns("4,12,*", new Border { Height = 24, Background = Ui.Primary, CornerRadius = new CornerRadius(2) }, new Border(), Ui.Text(labels[index], 20, true)), fields), 32);
            card.Background = Ui.Surface; card.MaxWidth = 900; card.CornerRadius = new CornerRadius(16); card.BoxShadow = BoxShadows.Parse("0 3 8 0 #18000000");
            content.Content = Ui.Scroll(card, 28);
        }
        for (var i = 0; i < order.Length; i++)
        {
            var index = i; var button = Ui.Button(labels[i], () => Select(index)); button.Tag = index; button.Classes.Clear(); button.Classes.Add("nav"); buttons.Add(button); nav.Children.Add(button);
        }
        var promo = Ui.Card(Ui.Stack(12, Ui.Text("Need more fields on your invoices?", 14, true, Ui.Primary), Ui.Text("Add PO number, project code, department, or any custom field.", 12, color: Ui.Muted), Ui.Button("See Options", () => { settingsTab = "Customize"; page.Content = SettingsView(); })), 14);
        var save = Ui.Button("Save", () => Model.SaveSettings("Invoice Settings"), true); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        var rail = new Border { Background = Ui.Surface, Child = Ui.Rows("*,Auto,Auto", Ui.Scroll(nav, 12), new Border { Padding = new Thickness(16), Child = promo }, new Border { Padding = new Thickness(16, 0, 16, 16), Child = save }) };
        var layout = Ui.Columns("240,*", rail, content);
        layout.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 900;
            layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "240,*"); layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");
            Grid.SetColumn(content, narrow ? 0 : 1); Grid.SetRow(content, narrow ? 1 : 0);
            rail.Height = narrow ? 140 : double.NaN; promo.IsVisible = !narrow;
        };
        Select(0); return Ui.Rows("Auto,*", Ui.AppBar("Invoice Settings"), layout);
    }
    // Builds the backup management screen and keeps its history list synchronized with completed file operations.
    private Control BackupView()
    {
        var history = Ui.Stack(14);
        void RenderHistory()
        {
            history.Children.Clear();
            if (backupHistory.Count == 0)
            {
                history.Children.Add(Ui.Empty("No backups found", "Create or import a backup to show it here."));
                return;
            }
            foreach (var backup in backupHistory.OrderByDescending(item => item.CreatedAt))
            {
                var icon = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(20),
                    Background = Brush.Parse(backup.IsDatabase ? "#2196F3" : "#4CAF60"),
                    Child = Ui.Icon(backup.IsDatabase ? "storage" : "code", 23, Brushes.White)
                };
                var menu = Ui.Button("⋮", () => { });
                menu.MinWidth = 38;
                ToolTip.SetTip(menu, "Backup actions");
                var flyout = new MenuFlyout();
                var restore = new MenuItem { Header = "Restore" };
                restore.Click += async (_, _) => await RunBackupFileAction(() => RestoreTrackedBackup(backup));
                var download = new MenuItem { Header = "Download" };
                download.Click += async (_, _) => await RunBackupFileAction(() => SaveTrackedBackupCopy(backup, "Download Backup"));
                var share = new MenuItem { Header = "Share" };
                share.Click += async (_, _) => await RunBackupFileAction(() => SaveTrackedBackupCopy(backup, "Share Backup"));
                var delete = new MenuItem { Header = "Delete" };
                delete.Click += (_, _) => Confirm("Delete Backup", $"Delete {backup.Name}?", async () => await RunBackupFileAction(() => DeleteTrackedBackup(backup)));
                flyout.Items.Add(restore);
                flyout.Items.Add(download);
                flyout.Items.Add(share);
                flyout.Items.Add(delete);
                menu.Flyout = flyout;
                var details = Ui.Stack(3,
                    Ui.Text(backup.Name, 15),
                    Ui.Text($"Size: {FormatFileSize(backup.Size)}", 12, color: Ui.Muted),
                    Ui.Text($"Created: {backup.CreatedAt:dd MMM yyyy HH:mm}", 12, color: Ui.Muted));
                var card = Ui.Card(Ui.Columns("Auto,16,*,Auto", icon, new Border(), details, menu), 16);
                card.Background = Brush.Parse("#F8F3FB");
                card.MaxWidth = 870;
                card.HorizontalAlignment = HorizontalAlignment.Stretch;
                history.Children.Add(card);
            }
        }
        RenderHistory();
        System.Collections.Specialized.NotifyCollectionChangedEventHandler historyChanged = (_, _) => RenderHistory();
        history.AttachedToVisualTree += (_, _) => backupHistory.CollectionChanged += historyChanged;
        history.DetachedFromVisualTree += (_, _) => backupHistory.CollectionChanged -= historyChanged;

        Button ActionButton(string label, string icon, Func<Task> action)
        {
            var button = Ui.Button(label, async () => await RunBackupFileAction(action));
            button.Content = Ui.Columns("Auto,8,*", Ui.Icon(icon, 18, Brush.Parse("#6750A4")), new Border(), Ui.Text(label, 14, color: Brush.Parse("#6750A4")));
            button.Background = Brush.Parse("#F5EFFA");
            button.BorderBrush = Brush.Parse("#E2D9E8");
            button.CornerRadius = new CornerRadius(22);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            return button;
        }

        var actions = Ui.Columns("*,16,*,16,*",
            ActionButton("Create DB Backup", "backup", CreateDatabaseBackupFile),
            new Border(),
            ActionButton("Export JSON", "download", CreateBackupFile),
            new Border(),
            ActionButton("Import Backup", "upload", ImportBackupFile));
        var body = Ui.Stack(16, actions, new Separator(), history);
        body.MaxWidth = 940;
        var refresh = Ui.Button("↻", RenderHistory);
        refresh.Background = Brushes.Transparent;
        refresh.BorderThickness = new Thickness(0);
        refresh.Foreground = Brushes.White;
        refresh.FontSize = 22;
        ToolTip.SetTip(refresh, "Refresh backup history");
        return Ui.Rows("Auto,*", Ui.AppBar("Backup Management", refresh), Ui.Scroll(body, 16));
    }

    // Formats a backup byte length for the history cards.
    private static string FormatFileSize(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:0.0} MB"
        : $"{Math.Max(0.1, bytes / 1024d):0.0} KB";

    // Adds or refreshes an entry in the visible backup history.
    private void TrackBackup(IStorageFile file, long size)
    {
        var existing = backupHistory.FirstOrDefault(item => item.Name.Equals(file.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) backupHistory.Remove(existing);
        backupHistory.Add(new BackupHistoryItem(file.Name, size, DateTime.Now, file.Name.EndsWith(".invoicedb", StringComparison.OrdinalIgnoreCase), file));
    }

    // Restores a backup selected from the history card action menu.
    private async Task RestoreTrackedBackup(BackupHistoryItem backup)
    {
        await using var stream = await backup.File.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var restored = backup.IsDatabase
            ? Model.RestoreDatabaseBackup(memory.ToArray())
            : Model.RestoreJsonBackup(Encoding.UTF8.GetString(memory.ToArray()));
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", Ui.Text(Model.Status), Ui.Button("Close", CloseOverlay, true));
    }

    // Saves another copy of a history backup using the platform file picker.
    private async Task SaveTrackedBackupCopy(BackupHistoryItem backup, string title)
    {
        await using var source = await backup.File.OpenReadAsync();
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory);
        var target = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = title,
            SuggestedFileName = backup.Name,
            DefaultExtension = backup.IsDatabase ? "invoicedb" : "json",
            FileTypeChoices = [new FilePickerFileType("LedgerNest backup") { Patterns = backup.IsDatabase ? ["*.invoicedb"] : ["*.json"] }]
        });
        if (target == null) return;
        await WriteBackupFileAsync(target, memory.ToArray());
        Model.Status = $"Saved {target.Name}.";
    }

    // Deletes a history backup from storage and removes its card after successful deletion.
    private async Task DeleteTrackedBackup(BackupHistoryItem backup)
    {
        await backup.File.DeleteAsync();
        backupHistory.Remove(backup);
        Model.Status = $"Deleted {backup.Name}.";
    }

    // Performs the run backup file action action for this screen or workflow.
    private async Task RunBackupFileAction(Func<Task> action)
    {
        var operationModel = Model;
        var version = operationModel.SessionVersion;
        try { await action(); }
        catch (Exception ex)
        {
            if (!CanContinueBackupOperation(operationModel, version)) return;
            operationModel.Status = $"Backup file error: {ex.Message}";
            ShowOverlay("Backup File Error", Ui.Text(operationModel.Status), Ui.Button("Close", CloseOverlay, true));
        }
    }

    // Performs the can continue backup operation action for this screen or workflow.
    private bool CanContinueBackupOperation(MainWindowViewModel model, long version) =>
        ReferenceEquals(DataContext, model) && model.CanContinueWorkspaceOperation(version);

    // Performs the create backup file action for this screen or workflow.
    private async Task CreateBackupFile()
    {
        var operationModel = Model;
        var sessionVersion = operationModel.SessionVersion;
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var backup = Model.CreateJsonBackup();
        if (backup.Length == 0) { ShowOverlay("Backup", Ui.Text(Model.Status), Ui.Button("Close", CloseOverlay, true)); return; }
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Create JSON Backup",
            SuggestedFileName = $"ledgernest_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON backup") { Patterns = ["*.json"], MimeTypes = ["application/json", "text/json"] }]
        });
        if (file == null || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await WriteBackupFileAsync(file, Encoding.UTF8.GetBytes(backup));
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        TrackBackup(file, Encoding.UTF8.GetByteCount(backup));
        ShowOverlay("Backup Created", Ui.Text($"{Model.Status} Saved {file.Name}."), Ui.Button("Close", CloseOverlay, true));
    }


    // Performs the create database backup file action for this screen or workflow.
    private async Task CreateDatabaseBackupFile()
    {
        var operationModel = Model;
        var sessionVersion = operationModel.SessionVersion;
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var backup = Model.CreateDatabaseBackup();
        if (backup.Length == 0) { ShowOverlay("Backup", Ui.Text(Model.Status), Ui.Button("Close", CloseOverlay, true)); return; }
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Create Database Backup",
            SuggestedFileName = $"ledgernest_backup_{DateTime.Now:yyyyMMdd_HHmmss}.invoicedb",
            DefaultExtension = "invoicedb",
            FileTypeChoices = [new FilePickerFileType("Database backup") { Patterns = ["*.invoicedb"], MimeTypes = ["application/octet-stream"] }]
        });
        if (file == null || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await WriteBackupFileAsync(file, backup);
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        TrackBackup(file, backup.LongLength);
        ShowOverlay("Backup Created", Ui.Text($"{Model.Status} Saved {file.Name}."), Ui.Button("Close", CloseOverlay, true));
    }

    // Writes through the storage-provider stream and retries transient Windows file-picker locks.
    private static async Task WriteBackupFileAsync(IStorageFile file, ReadOnlyMemory<byte> contents)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                await BackupStreamWriter.WriteAsync(stream, contents);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                await Task.Delay(150 * attempt);
            }
        }
    }

    // Imports either a JSON export or a complete database backup selected by the user.
    private async Task ImportBackupFile()
    {
        var operationModel = Model;
        var sessionVersion = operationModel.SessionVersion;
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Import Backup",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("LedgerNest backup") { Patterns = ["*.json", "*.invoicedb"], MimeTypes = ["application/json", "application/octet-stream"] }]
        });
        if (files.Count == 0 || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await using var stream = await files[0].OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var restored = files[0].Name.EndsWith(".invoicedb", StringComparison.OrdinalIgnoreCase)
            ? operationModel.RestoreDatabaseBackup(memory.ToArray())
            : operationModel.RestoreJsonBackup(Encoding.UTF8.GetString(memory.ToArray()));
        if (restored) TrackBackup(files[0], memory.Length);
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", Ui.Text(Model.Status), Ui.Button("Close", CloseOverlay, true));
        page.Content = Model.CanAccessWorkspace ? SettingsView() : null;
    }

    // Performs the restore database backup file action for this screen or workflow.
    private async Task RestoreDatabaseBackupFile()
    {
        var operationModel = Model;
        var sessionVersion = operationModel.SessionVersion;
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Restore Database Backup",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Database backup") { Patterns = ["*.invoicedb"], MimeTypes = ["application/octet-stream"] }]
        });
        if (files.Count == 0 || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await using var stream = await files[0].OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var restored = operationModel.RestoreDatabaseBackup(memory.ToArray());
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", Ui.Text(Model.Status), Ui.Button("Close", CloseOverlay, true));
        page.Content = Model.CanAccessWorkspace ? SettingsView() : null;
    }

    // Performs the restore backup file action for this screen or workflow.
    private async Task RestoreBackupFile()
    {
        var operationModel = Model;
        var sessionVersion = operationModel.SessionVersion;
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Restore JSON Backup",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON backup") { Patterns = ["*.json"], MimeTypes = ["application/json", "text/json"] }]
        });
        if (files.Count == 0 || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var json = await reader.ReadToEndAsync();
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var restored = operationModel.RestoreJsonBackup(json);
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", Ui.Text(Model.Status), Ui.Button("Close", CloseOverlay, true));
        page.Content = Model.CanAccessWorkspace ? SettingsView() : null;
    }
    // Performs the customization view action for this screen or workflow.
    private Control CustomizationView()
    {
        var cards = Ui.Stack(20, Ui.Text("MADE FOR YOUR BUSINESS", 12, true, Ui.Primary), Ui.Text($"Customize {Branding.Name}", 28, true));
        foreach (var (title, description) in new[] { ("Custom PDF Template", "An invoice design tailored to your business and branding."), ("Custom Fields", "Capture the additional details your business needs."), ("White Label", "Your brand, logo and identity throughout the application."), ("Industry Build", "A tailored workflow for your industry.") }) cards.Children.Add(Ui.Card(Ui.Stack(12, Ui.Text(title, 20, true), Ui.Text(description, 14, color: Ui.Muted), Ui.Button("Request Customization")), 24));
        cards.MaxWidth = 900; return Ui.Scroll(cards, 28);
    }
    // Performs the software info action for this screen or workflow.
    private Control SoftwareInfo() => Ui.Rows("Auto,*", Ui.AppBar("Software Information"), Ui.Scroll(Ui.Stack(24, Ui.Logo(), Ui.Card(Ui.Stack(18, Ui.Text("App Details", 18, true), Ui.Text($"App Name       {Branding.Name}"), Ui.Text("Platform          Desktop"), Ui.Text("License           See legacy LICENSE"))), Ui.Card(Ui.Stack(18, Ui.Text("Developer", 18, true), Ui.Text(Branding.Tagline), Ui.Button("Check for Updates"))), Ui.Button("Change Password", ShowChangePassword), Ui.Button("First-time Setup", ShowOnboarding)), 28));
}

// Describes a backup shown in the current Backup Management history.
internal sealed record BackupHistoryItem(string Name, long Size, DateTime CreatedAt, bool IsDatabase, IStorageFile File);
