using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed record InvoiceLongTextViewModel(FormField Field, string Icon, IRelayCommand ExpandCommand)
{
    public string ExpandLabel => "Expand " + Field.Label;
}

public sealed partial class ExpandedTextViewModel : ObservableObject
{
    [ObservableProperty] private string text;
    [ObservableProperty] private double editorHeight = 300;
    public int MaximumLength { get; }
    public bool IsReadOnly { get; }
    public string CharacterCount => $"{Text.Length}/{MaximumLength}";
    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(CharacterCount));
    public ExpandedTextViewModel(string text, int maximumLength = 10000, bool isReadOnly = false)
    { this.text = text; MaximumLength = maximumLength; IsReadOnly = isReadOnly; }
}
