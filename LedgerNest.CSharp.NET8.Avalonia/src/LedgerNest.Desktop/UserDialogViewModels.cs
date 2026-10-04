namespace LedgerNest.Desktop;

public sealed record UserDetailsViewModel(string Username, string Role, string Note)
{
    public string Initial => Username.Length == 0 ? "?" : Username[..1].ToUpperInvariant();
}
public sealed record UserEditorViewModel(FormFieldViewModel Username, FormFieldViewModel Role, bool EditingSelf);
public sealed record UserPasswordViewModel(string Username, FormFieldsViewModel Fields);
public sealed record DialogIcon(string Glyph);
