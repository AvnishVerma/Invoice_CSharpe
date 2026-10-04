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
    public ExpandedTextViewModel(string text) => this.text = text;
}
