namespace LedgerNest.Desktop;

public sealed record EmptyStateViewModel(string Title, string Subtitle, string Icon)
{
    public bool IsCart => Icon == "shopping_cart";
}
