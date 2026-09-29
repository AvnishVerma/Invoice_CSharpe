using Avalonia.Automation;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Maps friendly invoice branding sizes to their persisted numeric values.</summary>
public sealed partial class InvoiceSizeChoiceView : UserControl
{
    private readonly FormField? field;
    private readonly (string Name, string Value)[] sizes = [];

    public InvoiceSizeChoiceView()
    {
        InitializeComponent();
    }

    public InvoiceSizeChoiceView(FormField field, string label, bool signature)
        : this()
    {
        this.field = field;
        sizes = signature
            ? [("Small", "35"), ("Medium", "50"), ("Large", "70")]
            : [("X-Small", "40"), ("Small", "60"), ("Medium", "90"), ("Large", "120")];

        CaptionText.Text = label;
        AutomationProperties.SetName(SizeSelector, field.Label);
        var selected = sizes.FirstOrDefault(size => size.Value == field.Value).Name;
        var names = sizes.Select(size => size.Name).ToList();
        if (selected == null)
        {
            selected = $"Custom ({field.Value})";
            names.Add(selected);
        }

        SizeSelector.ItemsSource = names;
        SizeSelector.SelectedItem = selected;
        SizeSelector.SelectionChanged += SelectionChanged;
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (field == null || SizeSelector.SelectedItem is not string selected) return;
        var value = sizes.FirstOrDefault(size => size.Name == selected).Value;
        if (value != null) field.Value = value;
    }
}
