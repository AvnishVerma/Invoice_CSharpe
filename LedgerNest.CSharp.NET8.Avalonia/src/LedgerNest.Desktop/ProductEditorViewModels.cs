using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed class ProductInputViewModel : ObservableObject
{
    public FormField Field { get; }
    public FormFieldViewModel DateAdapter { get; }
    public string Icon { get; }
    public string Help { get; }
    public bool HasHelp => Help.Length > 0;
    public bool HasIcon => Icon.Length > 0;
    public bool IsCheck => Field.Kind == "toggle";
    public bool IsChoice => Field.Kind == "choice";
    public bool IsDate => Field.Kind == "date";
    public bool IsText => !IsCheck && !IsChoice && !IsDate;
    public bool IsMultiline => Field.Kind == "multiline";
    public bool ShowCaption => Field.Value.Length > 0 || IsChoice;
    public bool ShowAliasHelp => Field.Label == "Alias Name (for invoice PDF)";
    private readonly Func<bool> enabled;
    private readonly Func<bool> visible;
    public bool IsEnabled => enabled();
    public bool IsVisible => visible();
    public ProductInputViewModel(FormField field, string icon = "", string help = "", Func<bool>? enabled = null, Func<bool>? visible = null)
    {
        Field = field; Icon = icon; Help = help;
        DateAdapter = new FormFieldViewModel(field);
        this.enabled = enabled ?? (() => true); this.visible = visible ?? (() => true);
    }
    public void Attach() { DateAdapter.Attach(); Field.PropertyChanged -= Changed; Field.PropertyChanged += Changed; }
    public void Detach() { DateAdapter.Detach(); Field.PropertyChanged -= Changed; }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FormField.Value)) return;
        if (Field.Error.Length > 0) Field.Validate();
        OnPropertyChanged(nameof(ShowCaption));
    }
    public void Refresh() { OnPropertyChanged(nameof(IsEnabled)); OnPropertyChanged(nameof(IsVisible)); }
}
public sealed class ProductEditorRowViewModel : ObservableObject
{
    public IReadOnlyList<ProductInputViewModel> Inputs { get; }
    public int Columns => Inputs.Count;
    public bool IsVisible => Inputs.Any(input => input.IsVisible);
    public string Help { get; }
    public bool HasHelp => Help.Length > 0;
    public ProductEditorRowViewModel(IReadOnlyList<ProductInputViewModel> inputs, string help = "") { Inputs = inputs; Help = help; }
    public void Refresh() { foreach (var input in Inputs) input.Refresh(); OnPropertyChanged(nameof(IsVisible)); }
}
public sealed record ProductEditorSectionViewModel(string Title, IReadOnlyList<ProductEditorRowViewModel> Rows);
public sealed class ProductEditorViewModel
{
    public IReadOnlyList<ProductEditorSectionViewModel> Sections { get; }
    public IReadOnlyList<ProductInputViewModel> Advanced { get; }
    public bool HasAdvanced => Advanced.Count > 0;
    private readonly FormField unlimited;
    private readonly FormField unit;
    public ProductEditorViewModel(FormField[] fields, Func<string, bool> visible)
    {
        FormField F(string label) => fields.Single(field => field.Label == label);
        unlimited = F("Unlimited stock"); unit = F("Unit");
        ProductInputViewModel Input(string label, string icon = "", string help = "") => new(F(label), icon, help,
            label == "Stock" ? () => !unlimited.IsChecked : null,
            label == "Custom unit" ? () => unit.Value == "Custom…" : null);
        ProductEditorRowViewModel Row(params ProductInputViewModel[] inputs) => new(inputs.Where(input => visible(input.Field.Label)).ToArray());
        ProductEditorSectionViewModel Section(string title, params ProductEditorRowViewModel[] rows) => new(title, rows.Where(row => row.Inputs.Count > 0).ToArray());
        Sections = new[] {
            Section("GENERAL", Row(Input("Product ID", "badge"), Input("Barcode", "qr_code_2")), Row(Input("Name", "inventory_2")),
                Row(Input("Alias Name (for invoice PDF)", "translate")), Row(Input("Description", "description")), Row(Input("HSN/SAC", "qr_code_2"))),
            Section("PRICING", Row(Input("Sale Price"), Input("Purchase Price")), Row(Input("Default Discount")),
                new ProductEditorRowViewModel(new[] { Input("Tax (%)", "percent"), Input("Price includes tax") }.Where(input => visible(input.Field.Label)).ToArray(), "Per-item tax mode only")),
            Section("INVENTORY", Row(Input("Stock", "inventory_2"), Input("Unit", "straighten")), Row(Input("Custom unit", "straighten")),
                Row(Input("Unlimited stock", help: "Track infinite stock for this product")))
        }.Where(section => section.Rows.Count > 0).ToArray();
        (string Label, string Icon)[] metadata = [("Storage Location", "location_on"), ("Container Number", "inventory_2"),
            ("Batch Number", "tag"), ("Expiry Date", "calendar_today"), ("Manufacture Date", "calendar_today"),
            ("Manufacturer Name", "factory"), ("Supplier Name", "local_shipping"), ("SKU Code", "qr_code_2"), ("Notes", "notes")];
        Advanced = metadata.Where(item => visible(item.Label)).Select(item => Input(item.Label, item.Icon)).ToArray();
    }
    public void Attach() { unlimited.PropertyChanged -= Changed; unit.PropertyChanged -= Changed; unlimited.PropertyChanged += Changed; unit.PropertyChanged += Changed; Refresh(); }
    public void Detach() { unlimited.PropertyChanged -= Changed; unit.PropertyChanged -= Changed; }
    private void Changed(object? sender, PropertyChangedEventArgs args) => Refresh();
    private void Refresh() { foreach (var row in Sections.SelectMany(section => section.Rows)) row.Refresh(); }
}
public sealed partial class RecordEditorFooterViewModel : ObservableObject
{
    public string Kind { get; }
    public bool IsProduct => Kind == "Product";
    public bool IsCustomer => Kind == "Customer";
    public bool IsNew { get; }
    public string SaveLabel => $"Save {Kind}";
    [ObservableProperty] private bool useDefault;
    [ObservableProperty] private bool addAnother;
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public RecordEditorFooterViewModel(string kind, bool isNew, Action cancel, Action<RecordEditorFooterViewModel> save)
    {
        Kind = kind; IsNew = isNew; CancelCommand = new RelayCommand(cancel); SaveCommand = new RelayCommand(() => save(this));
    }
}
