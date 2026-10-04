using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed record OnboardingActions(OnboardingViewModel Model);

public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    private readonly Action close;
    private readonly FormField[][] groups;
    private readonly FormFieldsViewModel[] forms;
    [ObservableProperty] private int step;
    public string Title => new[] { "Company", "Invoice", "Appearance", "You're all set!" }[Step];
    public bool IsComplete => Step == 3;
    public bool IsCompany => Step == 0;
    public bool IsInvoice => Step == 1;
    public bool IsAppearance => Step == 2;
    public string BackLabel => Step == 0 ? "Cancel" : "Back";
    public string ContinueLabel => IsComplete ? "Get Started" : "Continue";
    public FormFieldsViewModel? Fields => IsComplete ? null : forms[Step];

    public OnboardingViewModel(MainWindowViewModel model, Action close)
    {
        this.model = model;
        this.close = close;
        groups = model.CreateOnboardingFields();
        forms = groups.Select(fields => new FormFieldsViewModel(fields.Select(field => new FormFieldViewModel(field)).ToArray(), 1)).ToArray();
    }

    partial void OnStepChanged(int value)
    {
        foreach (var name in new[] { nameof(Title), nameof(IsComplete), nameof(IsCompany), nameof(IsInvoice),
            nameof(IsAppearance), nameof(BackLabel), nameof(ContinueLabel), nameof(Fields) }) OnPropertyChanged(name);
    }

    [RelayCommand] private void Back() { if (Step == 0) close(); else Step--; }
    [RelayCommand] private void Continue()
    {
        if (IsComplete)
        {
            if (model.CompleteOnboarding(groups)) close();
            else Step = groups[0].Any(field => field.Error.Length > 0) ? 0 : 1;
            return;
        }
        if (groups[Step].Select(field => field.Validate()).ToArray().All(valid => valid)) Step++;
    }
}
