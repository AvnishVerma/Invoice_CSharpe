using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Input.Platform;
using LedgerNest.Desktop.Views;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private Control LicenseWorkspaceContent(Control body)
    {
        var model = Model;
        var notice = new ContentControl();
        var manage = Ui.Button("Manage License", () =>
        {
            settingsTab = "License";
            if (model.Title == "Settings") ShowPage(); else model.NavigateCommand.Execute("Settings");
        });
        var caption = Ui.LocalText("", 13);
        var banner = new Border { Padding = new Thickness(12.8, 6.4), Background = Ui.Palette("#FFF4E3", "#3F321E"), Child = Ui.Columns("*,12,Auto", caption, new Border(), manage) };
        notice.Content = banner;
        void Refresh()
        {
            notice.IsVisible = !model.CanMakeBusinessChanges || model.LicenseStatus.State == LicenseState.Trial;
            caption.Text = model.CanMakeBusinessChanges
                ? "Trial active · Expires " + model.LicenseStatus.Claims?.ExpiresAtUtc?.ToLocalTime().ToString("dd MMM yyyy")
                : "Read-only · Activate a license to create or change business records.";
        }
        var root = Ui.Rows("Auto,*", notice, body);
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == nameof(model.LicenseStatus)) Refresh(); };
        root.AttachedToVisualTree += (_, _) => model.PropertyChanged += changed;
        root.DetachedFromVisualTree += (_, _) => model.PropertyChanged -= changed;
        Refresh();
        return root;
    }

    private Control LicenseSettingsView()
    {
        var model = Model;
        model.RefreshLicense();
        var summary = new ContentControl();
        void RenderStatus()
        {
            var license = model.LicenseStatus;
            var title = license.State switch
            {
                LicenseState.Missing => "Not activated", LicenseState.NotConfigured => "Activation unavailable",
                LicenseState.NotYetValid => "Not yet active", LicenseState.WrongDevice => "Different device",
                LicenseState.ClockError => "Check device time", LicenseState.StorageError => "License unavailable",
                _ => license.State.ToString()
            };
            var fields = Ui.Stack(9.6, Ui.Text(title, 22, true), Ui.Text(license.Message, 14));
            if (license.Claims is { } claims)
            {
                fields.Children.Add(Ui.LocalText("Licensed to: " + claims.Customer, 15, true));
                fields.Children.Add(Ui.LocalText("License ID: " + claims.LicenseId, 12, color: Ui.Muted));
                fields.Children.Add(Ui.LocalText("Expires: " + (claims.ExpiresAtUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm zzz") ?? "Perpetual"), 14));
            }
            fields.Children.Add(Ui.Text(model.CanMakeBusinessChanges ? "Business changes are enabled." : "Existing records, PDF exports and backups remain available. A valid license is required to create or change business records.", 13, color: Ui.Muted));
            summary.Content = Ui.Card(fields, 22);
        }
        var device = new TextBox { Text = model.LicenseDeviceId, IsReadOnly = true, MinHeight = 36.8, TextWrapping = TextWrapping.Wrap };
        Avalonia.Automation.AutomationProperties.SetName(device, "License Device ID");
        var copy = Ui.Button("Copy Device ID", () => { });
        copy.IsEnabled = model.LicenseDeviceId.Length > 0;
        copy.Click += async (_, _) =>
        {
            if (Clipboard != null) { await Clipboard.SetTextAsync(model.LicenseDeviceId); model.Status = "Device ID copied."; }
        };
        var paste = new TextBox { PlaceholderText = "Paste a license document", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96, MaxLength = LicenseFeatures.MaximumFileBytes };
        Avalonia.Automation.AutomationProperties.SetName(paste, "License document");
        var activate = Ui.Button("Activate License", () => { if (model.ActivateLicense(paste.Text ?? "")) paste.Text = ""; }, true);
        var import = Ui.Button("Import License File", () => { }, true);
        import.Click += async (_, _) =>
        {
            import.IsEnabled = false;
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import LedgerNest License", AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("LedgerNest license") { Patterns = ["*.ledgerlicense", "*.json"] }]
                });
                if (files.Count == 0) return;
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                var buffer = new char[LicenseFeatures.MaximumFileBytes + 1];
                var length = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                if (length > LicenseFeatures.MaximumFileBytes) { model.Status = "License files must be no larger than 64 KB."; return; }
                model.ActivateLicense(new string(buffer, 0, length));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { model.Status = "The license file could not be opened. Check the file and try again."; }
            finally { import.IsEnabled = true; }
        };
        var content = Ui.Stack(16, summary,
            Ui.Card(Ui.Stack(9.6, Ui.LocalText("Activate this device", 18, true),
                Ui.LocalText("Send this Device ID to your software provider to receive a trial, paid or renewal license.", 14, color: Ui.Muted),
                device, copy, import), 22),
            Ui.Card(Ui.Stack(9.6, Ui.LocalText("Or paste your license", 18, true), paste, activate), 22),
            Ui.Button("Refresh License Status", model.RefreshLicense));
        content.MaxWidth = 820;
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == nameof(model.LicenseStatus)) RenderStatus(); };
        content.AttachedToVisualTree += (_, _) => model.PropertyChanged += changed;
        content.DetachedFromVisualTree += (_, _) => model.PropertyChanged -= changed;
        RenderStatus();
        return Ui.Rows("Auto,*", Ui.AppBar("License & Activation"), Ui.Scroll(content, 28));
    }
}
