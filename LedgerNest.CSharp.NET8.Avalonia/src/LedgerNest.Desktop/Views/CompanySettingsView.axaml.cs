using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace LedgerNest.Desktop.Views;

public sealed partial class CompanySettingsView : UserControl
{
    public CompanySettingsView() => InitializeComponent();

    private async void BrowseLogo(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not CompanySettingsViewModel model || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Company Logo", FileTypeFilter = [FilePickerFileTypes.ImageAll] });
        if (files.Count == 0) return;
        await using var input = await files[0].OpenReadAsync();
        using var bytes = new MemoryStream();
        await input.CopyToAsync(bytes);
        model.SetLogo(bytes.ToArray());
    }
}
