using System.Text.Json;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop;

public sealed record ManualSection(string Heading, string Text);
public sealed record ManualTopic(string Title, string Summary, ManualSection[] Sections);

/// <summary>Offline help content packaged with the application; contains no business state.</summary>
public sealed partial class UserManualViewModel : ObservableObject
{
    public IReadOnlyList<ManualTopic> Topics { get; }
    public IReadOnlyList<ManualTopic> FilteredTopics => Topics.Where(topic =>
        string.IsNullOrWhiteSpace(Search) ||
        (topic.Title + " " + topic.Summary + " " + string.Join(" ", topic.Sections.Select(s => s.Heading + " " + s.Text)))
            .Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray();
    public bool HasNoResults => FilteredTopics.Count == 0;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private ManualTopic? selectedTopic;

    public UserManualViewModel()
    {
        using var source = AssetLoader.Open(new Uri("avares://LedgerNest.Desktop/Assets/UserManual.json"));
        Topics = JsonSerializer.Deserialize<ManualTopic[]>(source) ?? [];
        SelectedTopic = Topics.FirstOrDefault();
    }

    partial void OnSearchChanged(string value)
    {
        var matches = FilteredTopics;
        if (SelectedTopic == null || !matches.Contains(SelectedTopic)) SelectedTopic = matches.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredTopics));
        OnPropertyChanged(nameof(HasNoResults));
    }
}
