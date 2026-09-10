using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class ProductItemDialogViewModel : ObservableObject
{
    private readonly InvoiceLineViewModel draft;
    private readonly Action<InvoiceLineViewModel> addLine;
    private readonly Action close;
    private bool completed;

    public string Title { get; }
    public string StockText { get; }
    public string DefaultPriceText { get; }
    public FormField Quantity { get; } = new("Quantity", "1", "number", required: true);
    public FormField Unit { get; }
    public FormField Discount { get; }
    public FormField Price { get; }
    public FormField ExtraCost { get; } = new("Extra Cost (optional)", "", "number");
    [ObservableProperty] private bool discountPerUnit = true;

    public ProductItemDialogViewModel(UiRecord product, Action<InvoiceLineViewModel> addLine, Action close)
    {
        draft = MainWindowViewModel.CreateProductLine(product);
        this.addLine = addLine;
        this.close = close;
        Title = $"{product.Name} (Rs. {draft.Price:0.0#})";
        StockText = product["Unlimited stock"] == "true" ? "Unlimited Stock" : $"Available Stock: {product["Stock"]}";
        DefaultPriceText = $"Default: Rs.{draft.Price:0.00}";
        Unit = new FormField("Unit (override)", draft.Unit, "choice",
            new[] { "None", "pcs", "kg", "g", "l", "m", "box", draft.Unit }.Distinct().ToArray());
        Discount = new FormField("Discount", draft.Discount.ToString(), "number", required: true);
        Price = new FormField("Unit Price (override)", draft.Price.ToString(), "number", required: true);
    }

    [RelayCommand]
    private void Add()
    {
        if (completed) return;
        if (!new[] { Quantity, Discount, Price, ExtraCost }.Select(field => field.Validate()).ToArray().All(valid => valid)) return;
        if (Quantity.Number <= 0) { Quantity.Error = "Quantity must be greater than zero."; return; }
        draft.Quantity = Quantity.Number;
        draft.Unit = Unit.Value;
        draft.Discount = Discount.Number;
        draft.DiscountPerUnit = DiscountPerUnit;
        draft.Price = Price.Number;
        draft.ExtraCost = ExtraCost.Number;
        completed = true;
        addLine(draft);
        close();
    }

    [RelayCommand]
    private void Cancel()
    {
        if (completed) return;
        completed = true;
        close();
    }
}
