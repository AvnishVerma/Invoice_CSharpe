namespace LedgerNest.Desktop;

public sealed class AuthenticationFormViewModel
{
    public string Title { get; }
    public string Subtitle { get; }
    public bool IsRecovery { get; }
    public FormFieldsViewModel Fields { get; }
    public FormFieldViewModel? Username { get; }

    public AuthenticationFormViewModel(string title, string subtitle, FormField[] fields, bool recovery = false)
    {
        Title = title;
        Subtitle = subtitle;
        IsRecovery = recovery;
        Username = recovery ? new FormFieldViewModel(fields[0]) : null;
        Fields = new FormFieldsViewModel((recovery ? fields.Skip(1) : fields)
            .Select(field => new FormFieldViewModel(field)).ToArray(), 1);
    }
}
