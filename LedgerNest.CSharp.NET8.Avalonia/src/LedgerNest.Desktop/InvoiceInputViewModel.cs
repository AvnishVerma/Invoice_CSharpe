using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop;

public sealed record InvoiceInputOption(string Value, string Caption);
public sealed class InvoiceInputViewModel : ObservableObject
{
    public FormField Field { get; }
    public string Label { get; }
    public string Icon { get; }
    public bool HasIcon => Icon.Length > 0;
    public bool HasLabel => Label.Length > 0;
    public bool IsChoice => Field.Kind == "choice";
    public bool IsTimeFormat => Field.Label == "Time Format";
    public int MaxLength => Field.Label == "Invoice Prefix" ? 25 : Field.MaxLength;
    public IReadOnlyList<InvoiceInputOption> Options { get; }
    public InvoiceInputOption? SelectedOption
    {
        get => Options.FirstOrDefault(option => option.Value == Field.Value);
        set { if (value != null) Field.Value = value.Value; }
    }
    public InvoiceInputViewModel(FormField field, string label, string icon)
    {
        Field = field;
        Label = label;
        Icon = icon;
        Options = field.Options.Select(value => new InvoiceInputOption(value, field.Label == "Time Format"
            ? value == "12 hour" ? "12-hour (2:30 PM)" : "24-hour (14:30)" : value)).ToArray();
    }
    public void Attach() { Field.PropertyChanged -= Changed; Field.PropertyChanged += Changed; }
    public void Detach() => Field.PropertyChanged -= Changed;
    private void Changed(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(FormField.Value)) OnPropertyChanged(nameof(SelectedOption)); }
}
