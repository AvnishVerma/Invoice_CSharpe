using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceImageUploadView : UserControl
{
    private InvoiceImageUploadViewModel? attached;
    public InvoiceImageUploadView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => AttachModel();
        DetachedFromVisualTree += (_, _) => { attached?.Detach(); attached = null; };
        DataContextChanged += (_, _) => { if (this.IsAttachedToVisualTree()) AttachModel(); };
    }
    private void AttachModel() { attached?.Detach(); attached = DataContext as InvoiceImageUploadViewModel; attached?.Attach(); }
    private async void UploadImage(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not InvoiceImageUploadViewModel model || TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = model.UploadLabel, AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PNG or JPEG image") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }]
        });
        if (files.Count == 0) return;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            model.SetImage(bytes.ToArray());
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { model.Field.Error = "The selected image could not be read."; }
    }
}
