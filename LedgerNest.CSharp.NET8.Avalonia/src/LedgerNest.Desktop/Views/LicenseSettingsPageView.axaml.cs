using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Views;

public sealed partial class LicenseSettingsPageView : UserControl
{
    public LicenseSettingsPageView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as LicenseSettingsViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as LicenseSettingsViewModel)?.Detach();
    }
    private async void CopyDeviceId(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not LicenseSettingsViewModel model || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(model.DeviceId); model.SetStatus("Device ID copied.");
    }
    private async void ImportLicense(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not LicenseSettingsViewModel model || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        model.IsImporting = true;
        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import LedgerNest License", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("LedgerNest license") { Patterns = ["*.ledgerlicense", "*.json"] }] });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync(); using var reader = new StreamReader(stream);
            var buffer = new char[LicenseFeatures.MaximumFileBytes + 1]; var length = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            if (length > LicenseFeatures.MaximumFileBytes) { model.SetStatus("License files must be no larger than 64 KB."); return; }
            model.ActivateImported(new string(buffer, 0, length));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { model.SetStatus("The license file could not be opened. Check the file and try again."); }
        finally { model.IsImporting = false; }
    }
}
