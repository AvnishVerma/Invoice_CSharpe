using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop;

public sealed partial class FormField : ObservableObject
{
    public string Label { get; }
    public string Kind { get; }
    public string[] Options { get; set; }
    public bool Required { get; }
    public int MaxLength { get; init; } = 1000;
    public string Icon { get; init; } = "";
    public string Help { get; init; } = "";
    [ObservableProperty] private string value;
    [ObservableProperty] private bool isChecked;
    [ObservableProperty] private string error = "";
    public FormField(string label, string value = "", string kind = "text", string[]? options = null, bool required = false)
    { Label = label; this.value = value; Kind = kind; Options = options ?? []; Required = required; isChecked = value == "true"; }
    // Performs the validate action for this screen or workflow.
    public bool Validate()
    {
        Error = Required && string.IsNullOrWhiteSpace(Value) ? $"{Label} is required." : "";
        if (Kind == "number" && Value.Length > 0 && (!decimal.TryParse(Value, NumberStyles.Number, CultureInfo.CurrentCulture, out var n) || n < 0))
            Error = "Enter a valid, non-negative number.";
        if (Kind is "text" or "multiline" && Label.Contains("Email") && Value.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(Value, out _)) Error = "Enter a valid email address.";
        return Error.Length == 0;
    }
    public decimal Number => decimal.TryParse(Value, out var n) ? n : 0;
}

public sealed record FormSection(string Title, FormField[] Fields);
public sealed class UiRecord
{
    public Guid Id { get; } = Guid.NewGuid();
    public int SourceId { get; init; }
    public Dictionary<string, string> Values { get; init; } = [];
    public string this[string name] => Values.GetValueOrDefault(name, "");
    public string Name => Values.GetValueOrDefault("Name", this["Username"]);
}

public sealed partial class InvoiceLineViewModel : ObservableObject
{
    public string ProductKey { get; init; } = "";
    public string ProductType { get; init; } = "Product";
    public LedgerNest.Domain.InvoiceLinePresentation? SavedPresentation { get; init; }
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string unit = "None";
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private decimal price;
    [ObservableProperty] private decimal taxRate;
    [ObservableProperty] private decimal discount;
    [ObservableProperty] private bool priceIncludesTax;
    [ObservableProperty] private bool discountPerUnit;
    [ObservableProperty] private decimal extraCost;
    public decimal Total => Quantity * Price - (DiscountPerUnit ? Discount * Quantity : Discount) + ExtraCost;
    // Performs the on discount per unit changed action for this screen or workflow.
    partial void OnDiscountPerUnitChanged(bool value) => OnPropertyChanged(nameof(Total));
    // Performs the on extra cost changed action for this screen or workflow.
    partial void OnExtraCostChanged(decimal value) => OnPropertyChanged(nameof(Total));
    // Performs the on quantity changed action for this screen or workflow.
    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(Total));
    // Performs the on price changed action for this screen or workflow.
    partial void OnPriceChanged(decimal value) => OnPropertyChanged(nameof(Total));
    // Performs the on tax rate changed action for this screen or workflow.
    partial void OnTaxRateChanged(decimal value) => OnPropertyChanged(nameof(Total));
    // Performs the on discount changed action for this screen or workflow.
    partial void OnDiscountChanged(decimal value) => OnPropertyChanged(nameof(Total));
}

public sealed partial class InvoiceCustomFieldDefinition : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string label = "";
}

public sealed record InvoiceCustomFieldEntry(string Id, FormField Field);
