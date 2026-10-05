using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using LedgerNest.Desktop.Views;
using Avalonia.Platform.Storage;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using LedgerNest.Infrastructure;
using LedgerNest.Desktop.Notifications;
using LedgerNest.Desktop.Updates;

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
            new("Company Info", "business"), new("Companies", "domain"), new("Backup", "backup"), new("Users", "people"), new("Permissions", "admin_panel_settings"),
            new("PDF Settings", "settings"), new("Invoice Settings", "receipt_long"), new("Product Details", "view_column"),
            new("Accessibility", "accessibility_new"), new("License", "lock"), new("Software Info", "info_outline")
        ];
        tabs = tabs.Where(tab => tab.Label switch
        {
            "Companies" => Model.CompanyManagement != null,
            "Users" => Model.HasPermission("User", "View"),
            "Permissions" => Model.HasPermission("Permission", "View"),
            _ => true
        }).ToArray();
        if (!tabs.Any(tab => tab.Label == settingsTab)) settingsTab = tabs.First().Label;
        return new SettingsPageView(new SettingsPageModel(tabs, settingsTab, SettingsContent, selected => settingsTab = selected));
    }

    // Performs the settings content selection action for this screen or workflow.
    private Control SettingsContent(string name)
    {
        if (name == "Users" && !Model.HasPermission("User", "View") || name == "Permissions" && !Model.HasPermission("Permission", "View"))
            return Ui.Empty("Access denied", "Your assigned roles do not allow this screen.");
        return name switch
    {
        "Company Info" => CompanySettingsView(),
        "Companies" => CompaniesView(),
        "PDF Settings" => PdfSettingsView(),
        "Product Details" => ProductDetailsSettingsView(),
        "Users" => new ManagementView(Model, "User", this),
        "Permissions" => new PermissionManagementView { DataContext = Model.PermissionManagement },
        "Backup" => BackupView(),
        "Accessibility" => new AccessibilitySettingsView(() => Model.SaveSettings("Accessibility")),
        "Software Info" => SoftwareInfo(),
        "License" => LicenseSettingsView(),
        _ => SettingsForm(name)
    };
    }
    // Performs the settings form action for this screen or workflow.
    private Control SettingsForm(string name)
    {
        if (name == "Invoice Settings") return InvoiceSettingsView();
        if (!Model.Settings.TryGetValue(name, out var sections)) return Ui.Empty("No settings available");
        return new SettingsFormPageView { DataContext = new SettingsFormPageViewModel(name, sections, () => Model.SaveSettings(name)) };
    }
    // Supplies backup state and existing file-operation behavior to the AXAML view.
    private Control BackupView()
    {
        ReloadBackupHistory();
        var model = new BackupManagementViewModel(backupHistory, ReloadBackupHistory,
            () => RunBackupFileAction(CreateDatabaseBackupFile),
            () => RunBackupFileAction(CreateBackupFile),
            () => RunBackupFileAction(ImportBackupFile),
            item => RunBackupFileAction(() => RestoreTrackedBackup(item)),
            item => RunBackupFileAction(() => SaveTrackedBackupCopy(item, "Download Backup")),
            item => RunBackupFileAction(() => SaveTrackedBackupCopy(item, "Share Backup")),
            item => Confirm("Delete Backup", $"Delete {item.Name}?", async () => await RunBackupFileAction(() => DeleteTrackedBackup(item))));
        return new BackupManagementView { DataContext = model };
    }
    // Loads the backup-history table each time the screen is opened or refreshed.
    private void ReloadBackupHistory()
    {
        backupHistory.Clear();
        foreach (var backup in Model.LoadBackupHistory().OrderByDescending(item => item.CreatedAt))
            backupHistory.Add(new BackupHistoryItem(
                backup.Name,
                backup.FilePath,
                backup.Size,
                backup.CreatedAt,
                backup.IsDatabase));
    }

    // Adds or refreshes an entry in the persisted backup-history table.
    private void TrackBackup(IStorageFile file, long size)
    {
        var filePath = file.Path.LocalPath;
        var existing = backupHistory.FirstOrDefault(item => item.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null) backupHistory.Remove(existing);
        var isDatabase = file.Name.EndsWith(".invoicedb", StringComparison.OrdinalIgnoreCase);
        var createdAt = DateTime.Now;
        backupHistory.Insert(0, new BackupHistoryItem(file.Name, filePath, size, createdAt, isDatabase));
        Model.RecordBackupHistory(file.Name, filePath, size, isDatabase);
    }

    // Restores a backup selected from the history card action menu.
    private async Task RestoreTrackedBackup(BackupHistoryItem backup)
    {
        var bytes = await File.ReadAllBytesAsync(backup.FilePath);
        var restored = backup.IsDatabase
            ? Model.RestoreDatabaseBackup(bytes)
            : Model.RestoreJsonBackup(Encoding.UTF8.GetString(bytes));
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
    }

    // Saves another copy of a history backup using the platform file picker.
    private async Task SaveTrackedBackupCopy(BackupHistoryItem backup, string title)
    {
        var bytes = await File.ReadAllBytesAsync(backup.FilePath);
        var target = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = title,
            SuggestedFileName = backup.Name,
            DefaultExtension = backup.IsDatabase ? "invoicedb" : "json",
            FileTypeChoices = [new FilePickerFileType("LedgerNest backup") { Patterns = backup.IsDatabase ? ["*.invoicedb"] : ["*.json"] }]
        });
        if (target == null) return;
        await WriteBackupFileAsync(target, bytes);
        Model.Status = $"Saved {target.Name}.";
    }

    // Deletes a history backup from storage and removes its card after successful deletion.
    private async Task DeleteTrackedBackup(BackupHistoryItem backup)
    {
        File.Delete(backup.FilePath);
        backupHistory.Remove(backup);
        Model.RemoveBackupHistory(backup.FilePath);
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
            ShowOverlay("Backup File Error", new DialogMessage(operationModel.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
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
        if (backup.Length == 0) { ShowOverlay("Backup", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)])); return; }
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Create JSON Backup",
            SuggestedFileName = operationModel.SuggestedBackupName("json"),
            SuggestedStartLocation = operationModel.CompanyBackupDirectory is { } jsonDirectory
                ? await StorageProvider.TryGetFolderFromPathAsync(jsonDirectory) : null,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON backup") { Patterns = ["*.json"], MimeTypes = ["application/json", "text/json"] }]
        });
        if (file == null || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await WriteBackupFileAsync(file, Encoding.UTF8.GetBytes(backup));
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        TrackBackup(file, Encoding.UTF8.GetByteCount(backup));
        ShowOverlay("Backup Created", new DialogMessage($"{Model.Status} Saved {file.Name}."), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
    }


    // Performs the create database backup file action for this screen or workflow.
    private async Task CreateDatabaseBackupFile()
    {
        var operationModel = Model;
        var sessionVersion = operationModel.SessionVersion;
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        var backup = Model.CreateDatabaseBackup();
        if (backup.Length == 0) { ShowOverlay("Backup", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)])); return; }
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Create Database Backup",
            SuggestedFileName = operationModel.SuggestedBackupName("invoicedb"),
            SuggestedStartLocation = operationModel.CompanyBackupDirectory is { } databaseDirectory
                ? await StorageProvider.TryGetFolderFromPathAsync(databaseDirectory) : null,
            DefaultExtension = "invoicedb",
            FileTypeChoices = [new FilePickerFileType("Database backup") { Patterns = ["*.invoicedb"], MimeTypes = ["application/octet-stream"] }]
        });
        if (file == null || !CanContinueBackupOperation(operationModel, sessionVersion)) return;
        await WriteBackupFileAsync(file, backup);
        if (!CanContinueBackupOperation(operationModel, sessionVersion)) return;
        TrackBackup(file, backup.LongLength);
        ShowOverlay("Backup Created", new DialogMessage($"{Model.Status} Saved {file.Name}."), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
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
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
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
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
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
        ShowOverlay(restored ? "Backup Restored" : "Restore Failed", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
        page.Content = Model.CanAccessWorkspace ? SettingsView() : null;
    }
    // Performs the customization view action for this screen or workflow.
    private Control CustomizationView()
    {
        return new ScreenScaffoldView(Ui.AppBar("Customize"), new CustomizationOffersView(Branding.Name));
    }
    // Shows application identity, update configuration, and notifications using AXAML bindings.
    private Control SoftwareInfo() => new SoftwareInformationView
    {
        DataContext = new SoftwareInformationViewModel(Model, () => CheckForUpdatesAsync(), OpenUpdateDownload, ShowChangePassword, ShowOnboarding)
    };


    // Provides an administrator-only workspace for preparing update and announcement manifests.
    private Control PublisherConsole()
    {
        return new PublisherConsoleFormView { DataContext = new PublisherConsoleViewModel(SaveGlobalUpdateManifestAsync) };
    }
    // Creates the publisher-controlled manifest that clients read from the configured HTTPS endpoint.
    private async Task SaveGlobalUpdateManifestAsync(string? versionText, string? downloadUrl, string? notes, string? announcementTitle, string? announcementMessage, string? notificationType)
    {
        if (!Version.TryParse(versionText?.Trim(), out var version))
        {
            toastService.Show("Manifest not created", "Enter a valid release version, such as 4.5.0.", ToastType.Warning);
            return;
        }

        var releaseUrl = downloadUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(releaseUrl) && (!Uri.TryCreate(releaseUrl, UriKind.Absolute, out var releaseUri) || releaseUri.Scheme != Uri.UriSchemeHttps))
        {
            toastService.Show("Manifest not created", "The download URL must use HTTPS.", ToastType.Warning);
            return;
        }

        var title = announcementTitle?.Trim() ?? "";
        var message = announcementMessage?.Trim() ?? "";
        if ((title.Length == 0) != (message.Length == 0))
        {
            toastService.Show("Manifest not created", "Enter both a notification title and message, or leave both blank.", ToastType.Warning);
            return;
        }

        var type = Enum.TryParse<NotificationType>(notificationType, out var parsedType) ? parsedType : NotificationType.Information;
        IReadOnlyList<GlobalNotification>? notifications = title.Length == 0
            ? null
            : [new GlobalNotification(Guid.NewGuid().ToString("N"), title, message, type, DateTimeOffset.UtcNow)];
        var manifest = new AppUpdateManifest(version.ToString(3), releaseUrl, notes?.Trim(), DateTimeOffset.UtcNow, false, notifications);
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save global update manifest",
            SuggestedFileName = "ledgernest-update-manifest.json",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("Update manifest") { Patterns = ["*.json"], MimeTypes = ["application/json"] }]
        });
        if (target == null) return;

        await using var stream = await target.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(HttpAppUpdateService.SerializeManifest(manifest));
        Model.Status = $"Saved {target.Name}. Upload it to the configured HTTPS manifest URL to publish globally.";
        toastService.Show("Manifest ready", "Upload the saved manifest to publish the update and announcement globally.", ToastType.Success);
        ShowPage();
    }
    // Opens the publisher's verified HTTPS release page after an explicit user action.
    private void OpenUpdateDownload()
    {
        if (!Uri.TryCreate(Model.LatestUpdateDownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            toastService.Show("Update download unavailable", "The release download link is invalid.", ToastType.Warning);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            toastService.Show("Update download unavailable", "Windows could not open the release download page.", ToastType.Error);
        }
    }
}

// Describes a backup shown in the current Backup Management history.
