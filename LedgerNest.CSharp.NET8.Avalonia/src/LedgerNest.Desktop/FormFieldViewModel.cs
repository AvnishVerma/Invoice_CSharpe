using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class FormFieldViewModel : ObservableObject
{
    public FormField Field { get; }
    public string Label { get; }
    public string Caption => Label + (Field.Required && !Label.EndsWith('*') ? " *" : "");
    public bool Compact { get; }
    public bool WithIcon { get; }
    public bool IsChoice => Field.Kind == "choice";
    public bool IsDate => Field.Kind == "date";
    public bool IsSlider => Field.Kind == "slider";
    public bool IsFile => Field.Kind == "file";
    public bool IsToggle => Field.Kind == "toggle";
    public bool IsText => !IsChoice && !IsDate && !IsSlider && !IsFile && !IsToggle;
    public bool IsMultiline { get; }
    public char PasswordChar => Field.Kind == "password" ? '●' : '\0';
    public bool ShowCaption => IsChoice || IsDate || Field.Value.Length > 0;
    public string DateText => DateTime.TryParse(Field.Value, out var date) ? date.ToString("dd/MM/yyyy") : "";
    public DateTime CalendarDisplayDate => SelectedDate ?? DateTime.Today;
    public DateTime? SelectedDate
    {
        get => DateTime.TryParse(Field.Value, out var date) ? date : null;
        set => Field.Value = value?.ToString("yyyy-MM-dd") ?? "";
    }
    public double SliderValue { get => (double)Field.Number; set => Field.Value = value.ToString("0"); }
    public string ImageStatus => Field.Value.Length == 0 ? "No image selected" : "Image selected";
    public bool HasHelp => Field.Help.Length > 0;

    public FormFieldViewModel(FormField field, string? label = null, bool singleLine = false, bool compact = true)
    {
        Field = field;
        Label = label ?? field.Label;
        WithIcon = label == null && field.Icon.Length > 0;
        Compact = compact;
        IsMultiline = field.Kind == "multiline" && !singleLine;
    }

    public void Attach()
    {
        Field.PropertyChanged -= FieldChanged;
        Field.PropertyChanged += FieldChanged;
        Refresh();
    }
    public void Detach() => Field.PropertyChanged -= FieldChanged;
    private void FieldChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FormField.Value)) Refresh();
    }
    private void Refresh()
    {
        OnPropertyChanged(nameof(ShowCaption)); OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(SelectedDate)); OnPropertyChanged(nameof(SliderValue)); OnPropertyChanged(nameof(ImageStatus));
        OnPropertyChanged(nameof(CalendarDisplayDate));
    }
    [RelayCommand] private void RemoveImage() { Field.Value = ""; Field.Error = ""; Refresh(); }
}

public sealed record FormFieldsRow(IReadOnlyList<FormFieldViewModel> Fields, int Columns);
public sealed record FormFieldsViewModel(IReadOnlyList<FormFieldViewModel> Fields, int Columns)
{
    public IReadOnlyList<FormFieldsRow> Rows { get; } = Fields.Chunk(Math.Max(1, Columns))
        .Select(fields => new FormFieldsRow(fields, Math.Max(1, Columns))).ToArray();
}
