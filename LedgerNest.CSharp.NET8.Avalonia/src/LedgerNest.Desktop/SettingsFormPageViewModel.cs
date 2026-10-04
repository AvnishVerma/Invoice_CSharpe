using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed record SettingsFormSection(string Title, FormFieldsViewModel Fields);

public sealed class SettingsFormPageViewModel
{
    public string Title { get; }
    public IReadOnlyList<SettingsFormSection> Sections { get; }
    public IRelayCommand SaveCommand { get; }
    public KeyboardShortcutsViewModel Shortcuts { get; } = new(true);
    public SettingsFormPageViewModel(string title, IEnumerable<FormSection> sections, Action save)
    {
        Title = title;
        Sections = sections.Select(section => new SettingsFormSection(section.Title,
            new FormFieldsViewModel(section.Fields.Select(field => new FormFieldViewModel(field)).ToArray(), 1))).ToArray();
        SaveCommand = new RelayCommand(save);
    }
}

public sealed record KeyboardShortcut(string Key, string Description);
public sealed class KeyboardShortcutsViewModel
{
    public IReadOnlyList<KeyboardShortcut> Items { get; }
    public KeyboardShortcutsViewModel(bool includePreview = false)
    {
        KeyboardShortcut[] shortcuts = [new("Ctrl + Q", "New invoice"), new("Ctrl + S", "Save invoice"),
            new("Ctrl + F", "Search products"), new("Ctrl + M", "Add custom item"), new("Ctrl + O", "Preview PDF"), new("Ctrl + P", "Print invoice")];
        Items = shortcuts.Where(shortcut => includePreview || shortcut.Key != "Ctrl + O").ToArray();
    }
}
