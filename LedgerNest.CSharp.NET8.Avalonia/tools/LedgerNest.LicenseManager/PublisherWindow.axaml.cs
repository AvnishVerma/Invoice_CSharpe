using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LedgerNest.LicenseManager;

public partial class PublisherWindow : Window
{
    public PublisherWindow()
    {
        InitializeComponent();
        DataContext = new PublisherViewModel(new WindowFiles(this));
        Closing += (_, e) =>
        {
            if (DataContext is PublisherViewModel { IsBusy: true }) e.Cancel = true;
        };
        Closed += (_, _) => (DataContext as PublisherViewModel)?.ClearPasswords();
    }

    private sealed class WindowFiles(Window window) : IPublisherFiles
    {
        private static readonly FilePickerFileType KeyFiles = new("PEM key files") { Patterns = ["*.pem"] };
        private static readonly FilePickerFileType LicenseFiles = new("LedgerNest licenses") { Patterns = ["*.ledgerlicense"] };

        public async Task<string?> OpenPrivateKeyAsync()
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new()
            { Title = "Select encrypted publisher signing key", AllowMultiple = false, FileTypeFilter = [KeyFiles] });
            return files.Count == 0 ? null : LocalPath(files[0]);
        }
        public async Task<string?> SelectKeyFolderAsync()
        {
            var folders = await window.StorageProvider.OpenFolderPickerAsync(new()
            { Title = "Choose a folder for your publisher keys", AllowMultiple = false });
            return folders.Count == 0 ? null : LocalPath(folders[0]);
        }
        public Task<string?> SaveLicenseAsync() => SaveAsync("Save customer license", "customer.ledgerlicense", "ledgerlicense", LicenseFiles);
        public Task<string?> SavePublicKeyAsync() => SaveAsync("Export public verification key", "public-key.pem", "pem", KeyFiles);

        private async Task<string?> SaveAsync(string title, string name, string extension, FilePickerFileType type)
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(new()
            { Title = title, SuggestedFileName = name, DefaultExtension = extension, FileTypeChoices = [type], ShowOverwritePrompt = true });
            return file == null ? null : LocalPath(file);
        }
        private static string LocalPath(IStorageItem item) => item.TryGetLocalPath()
            ?? throw new ArgumentException("Choose a local Windows file or folder.");
    }
}
