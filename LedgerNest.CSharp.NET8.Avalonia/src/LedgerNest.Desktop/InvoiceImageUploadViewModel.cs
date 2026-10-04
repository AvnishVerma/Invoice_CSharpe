using System.ComponentModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed class InvoiceImageUploadViewModel : ObservableObject
{
    private Bitmap? preview;
    private readonly Func<byte[], bool> setImage;
    public FormField Field { get; }
    public string Name { get; }
    public string UploadLabel => "Upload " + Name;
    public string RemoveLabel => "Remove " + Name;
    public Bitmap? Preview { get => preview; private set { SetProperty(ref preview, value); OnPropertyChanged(nameof(HasImage)); } }
    public bool HasImage => Preview != null;
    public IRelayCommand RemoveCommand { get; }
    public InvoiceImageUploadViewModel(FormField field, string name, Func<byte[], bool> setImage)
    {
        Field = field;
        Name = name;
        this.setImage = setImage;
        RemoveCommand = new RelayCommand(() => Field.Value = "");
    }
    public bool SetImage(byte[] bytes) => setImage(bytes);
    public void Attach() { Field.PropertyChanged -= Changed; Field.PropertyChanged += Changed; Refresh(); }
    public void Detach() { Field.PropertyChanged -= Changed; ClearPreview(); }
    private void Changed(object? sender, PropertyChangedEventArgs args) { if (args.PropertyName == nameof(FormField.Value)) Refresh(); }
    private void ClearPreview() { var previous = Preview; Preview = null; previous?.Dispose(); }
    private void Refresh() { ClearPreview(); Preview = Views.Ui.LoadLogo(Field.Value); }
}
