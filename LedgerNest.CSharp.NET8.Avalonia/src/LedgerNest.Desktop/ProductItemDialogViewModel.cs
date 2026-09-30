using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class ProductItemDialogViewModel : ObservableObject
{
    private readonly InvoiceLineViewModel draft;
    private readonly Action<InvoiceLineViewModel> addLine;
    private readonly Action close;
    private readonly Func<InvoiceLineViewModel, bool>? acceptLine;
    private readonly bool allowFractional;
    private bool completed;
    private readonly Dictionary<string, SellingUnitChoice> sellingUnits;

    public string Title { get; }
    public string StockText { get; }
    public string DefaultPriceText { get; }
    public bool ShowStock { get; }
    public bool ShowUnit { get; }
    public bool ShowDiscount { get; }
    public bool ShowExtraCost { get; }
    public FormField Quantity { get; } = new("Quantity", "1", "number", required: true);
    public FormField Unit { get; }
    public FormField Discount { get; }
    public FormField Price { get; }
    public FormField ExtraCost { get; } = new("Extra Cost (optional)", "", "number");
    [ObservableProperty] private bool discountPerUnit = true;

    public ProductItemDialogViewModel(UiRecord product, Action<InvoiceLineViewModel> addLine, Action close, Func<InvoiceLineViewModel, bool>? acceptLine = null, bool allowFractional = true, Func<string, bool>? fieldVisible = null, IEnumerable<string>? unitOptions = null, IEnumerable<SellingUnitChoice>? sellingUnitOptions = null)
    {
        ShowStock = fieldVisible?.Invoke("Stock") ?? true;
        ShowUnit = fieldVisible?.Invoke("Unit") ?? true;
        ShowDiscount = fieldVisible?.Invoke("Default Discount") ?? true;
        ShowExtraCost = fieldVisible?.Invoke("Extra Cost") ?? true;
        draft = MainWindowViewModel.CreateProductLine(product);
        this.addLine = addLine;
        this.close = close;
        this.acceptLine = acceptLine;
        this.allowFractional = allowFractional;
        Title = $"{product.Name} ({CurrencyDisplay.Format(draft.Price, "0.0#")})";
        var hasUnlimitedStock = bool.TryParse(product["Unlimited stock"], out var unlimitedStock) && unlimitedStock;
        StockText = hasUnlimitedStock ? "Unlimited Stock" : $"Available Stock: {product["Stock"]}";
        DefaultPriceText = $"Default: {CurrencyDisplay.Format(draft.Price)}";
        sellingUnits = (sellingUnitOptions ?? []).GroupBy(choice => choice.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        Unit = new FormField("Unit (override)", draft.Unit, "choice",
            (sellingUnits.Count > 0 ? sellingUnits.Keys : unitOptions ?? new[] { "None", draft.Unit }).Append(draft.Unit).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        Discount = new FormField("Discount", draft.Discount.ToString(), "number", required: true);
        Price = new FormField("Unit Price (override)", draft.Price.ToString(), "number", required: true);
        Unit.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FormField.Value) && sellingUnits.TryGetValue(Unit.Value, out var choice))
                Price.Value = choice.Price.ToString(System.Globalization.CultureInfo.CurrentCulture);
        };
    }

    [RelayCommand]
    // Performs the add action for this screen or workflow.
    private void Add()
    {
        if (completed) return;
        if (!new[] { Quantity, Discount, Price, ExtraCost }.Select(field => field.Validate()).ToArray().All(valid => valid)) return;
        if (Quantity.Number <= 0) { Quantity.Error = "Quantity must be greater than zero."; return; }
        if (!allowFractional && decimal.Truncate(Quantity.Number) != Quantity.Number) { Quantity.Error = "Enter a whole-number quantity."; return; }
        draft.Quantity = Quantity.Number;
        draft.Unit = Unit.Value;
        if (sellingUnits.TryGetValue(Unit.Value, out var choice))
        {
            draft.SellingUnitId = choice.SellingUnitId;
            draft.UnitConversionFactor = choice.ConversionFactor;
        }
        draft.Discount = Discount.Number;
        draft.DiscountPerUnit = DiscountPerUnit;
        draft.Price = Price.Number;
        draft.ExtraCost = ShowExtraCost ? ExtraCost.Number : 0;
        if (acceptLine != null && !acceptLine(draft)) return;
        completed = true;
        if (acceptLine == null) addLine(draft);
        close();
    }

    [RelayCommand]
    // Performs the cancel action for this screen or workflow.
    private void Cancel()
    {
        if (completed) return;
        completed = true;
        close();
    }
}
