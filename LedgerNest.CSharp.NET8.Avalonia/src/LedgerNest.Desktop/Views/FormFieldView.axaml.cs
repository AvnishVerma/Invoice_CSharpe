using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace LedgerNest.Desktop.Views;

public sealed partial class FormFieldView : UserControl
{
    public FormFieldView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as FormFieldViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as FormFieldViewModel)?.Detach();
    }
    private Button? CalendarTrigger => this.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "CalendarButton");
    private void OpenCalendar(object? sender, PointerPressedEventArgs args)
    {
        if (CalendarTrigger is { } trigger) trigger.Flyout?.ShowAt(trigger);
    }
    private void DateSelected(object? sender, SelectionChangedEventArgs args) => CalendarTrigger?.Flyout?.Hide();
    private async void UploadImage(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not FormFieldViewModel model || TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = model.Field.Label, AllowMultiple = false, FileTypeFilter = [FilePickerFileTypes.ImageAll]
        });
        if (files.Count == 0) return;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            if (bytes.Length > 2 * 1024 * 1024) { model.Field.Error = "Logo must be 2 MB or smaller."; return; }
            var value = "base64:" + Convert.ToBase64String(bytes.ToArray());
            using var bitmap = Ui.LoadLogo(value);
            if (bitmap == null || bitmap.PixelSize.Width > 1080 || bitmap.PixelSize.Height > 1080)
            {
                model.Field.Error = "Choose an image up to 1080 × 1080 pixels.";
                return;
            }
            model.Field.Value = value;
            model.Field.Error = "";
        }
        catch (IOException) { model.Field.Error = "The image could not be read."; }
    }
}
