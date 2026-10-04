using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed class SegmentedChoiceViewModel
{
    private readonly FormField field;
    public IReadOnlyList<SegmentedOptionViewModel> Options { get; }
    public SegmentedChoiceViewModel(FormField field)
    {
        this.field = field;
        Options = field.Options.Select(option => new SegmentedOptionViewModel(field, option)).ToArray();
    }
    public void Attach() { field.PropertyChanged -= Changed; field.PropertyChanged += Changed; Refresh(); }
    public void Detach() => field.PropertyChanged -= Changed;
    private void Changed(object? sender, PropertyChangedEventArgs args) { if (args.PropertyName == nameof(FormField.Value)) Refresh(); }
    private void Refresh() { foreach (var option in Options) option.Refresh(); }
}

public sealed class SegmentedOptionViewModel : ObservableObject
{
    private readonly FormField field;
    public string Value { get; }
    public bool IsSelected => this.field.Value == Value;
    public IRelayCommand SelectCommand { get; }
    public SegmentedOptionViewModel(FormField field, string value)
    {
        this.field = field;
        Value = value;
        SelectCommand = new RelayCommand(() => field.Value = Value);
    }
    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}
