namespace LedgerNest.Desktop;

// Presentation-only currency formatting. Values are never converted between currencies.
public static class CurrencyDisplay
{
    public static Func<string> SelectedCurrency { private get; set; } = () => LegacyChoices.Currencies[0];
    public static string Current => SelectedCurrency();
    public static string Symbol(string? currency = null) => (string.IsNullOrWhiteSpace(currency) ? Current : currency).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    public static string Code(string? currency = null) => (string.IsNullOrWhiteSpace(currency) ? Current : currency).Split('—')[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
    public static bool UsesIndianNumbering(string? currency = null) => Code(currency).Equals("INR", StringComparison.OrdinalIgnoreCase);
    public static string Format(decimal value, string format = "0.00", string? currency = null) => $"{Symbol(currency)} {value.ToString(format)}";
}
