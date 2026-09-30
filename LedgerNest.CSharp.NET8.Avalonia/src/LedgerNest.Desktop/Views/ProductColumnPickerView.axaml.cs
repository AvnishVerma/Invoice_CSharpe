using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductColumnPickerView : UserControl
{
    public ProductColumnOption[] Options { get; private set; } = [];

    public ProductColumnPickerView() { InitializeComponent(); }

    public ProductColumnPickerView(IEnumerable<(string Label, bool Selected)> options, Action<string, bool> changed) : this()
    {
        Options = options.Select(option => new ProductColumnOption(option.Label, option.Selected)).ToArray();
        foreach (var option in Options)
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ProductColumnOption.IsSelected)) return;
                UpdateAvailability();
                changed(option.Label, option.IsSelected);
            };
        UpdateAvailability();
        DataContext = this;
    }

    private void UpdateAvailability()
    {
        var count = Options.Count(option => option.IsSelected && option.Label != "Alias Name");
        foreach (var option in Options)
            option.CanSelect = option.IsSelected || option.Label == "Alias Name" || count < 10;
    }
}

public sealed class ProductColumnOption(string label, bool selected) : ObservableObject
{
    private bool isSelected = selected;
    private bool canSelect = true;
    public string Label { get; } = label;
    public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }
    public bool CanSelect { get => canSelect; set => SetProperty(ref canSelect, value); }
}
