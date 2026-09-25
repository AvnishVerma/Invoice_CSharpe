using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Platform;

namespace LedgerNest.Desktop;

/// <summary>Presentation-only translations; model keys and user-entered values remain unchanged.</summary>
public sealed class UiLocalization : AvaloniaObject
{
    public static readonly AttachedProperty<string> TextProperty = AvaloniaProperty.RegisterAttached<UiLocalization, TextBlock, string>("Text", "");
    static UiLocalization() => TextProperty.Changed.AddClassHandler<TextBlock>((control, args) => Bind(control, TextBlock.TextProperty, args.NewValue as string ?? ""));
    public static string GetText(TextBlock control) => control.GetValue(TextProperty);
    public static void SetText(TextBlock control, string value) => control.SetValue(TextProperty, value);
    public static readonly string[] Languages = ["English", "हिन्दी", "नेपाली", "བོད་ཡིག", "Español", "Français", "中文"];
    private static readonly string[] Codes = ["en", "hi", "ne", "bo", "es", "fr", "zh"];
    private static readonly Dictionary<string, Dictionary<string, string>> catalogs = [];
    private static readonly HashSet<string> registered = [];
    private static Dictionary<string, string>? englishKeys;
    public static string Language { get; private set; } = "English";

    public static string Normalize(string? language) => Languages.Contains(language) ? language! : "English";

    private static Dictionary<string, string> Catalog(string code)
    {
        if (catalogs.TryGetValue(code, out var catalog)) return catalog;
        using var stream = AssetLoader.Open(new Uri($"avares://LedgerNest.Desktop/Assets/Localization/{code}.json"));
        using var json = JsonDocument.Parse(stream);
        catalog = json.RootElement.EnumerateObject().Where(p => !p.Name.StartsWith('@') && p.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
        return catalogs[code] = catalog;
    }

    private static string Match(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static string Translate(string source)
    {
        if (Language == "English" || string.IsNullOrEmpty(source)) return source;
        englishKeys ??= Catalog("en").Where(p => !p.Value.Contains('{')).GroupBy(p => Match(p.Value)).ToDictionary(g => g.Key, g => g.First().Key);
        englishKeys.TryGetValue(Match(source), out var key);
        var code = Codes[Array.IndexOf(Languages, Language)];
        if (key == null || !Catalog(code).TryGetValue(key, out var translated) || translated.Contains('{')) return source;
        return translated + (source.EndsWith(" *") ? " *" : source.EndsWith(':') ? ":" : "");
    }

    public static void Apply(string language)
    {
        Language = Normalize(language);
        if (Avalonia.Application.Current is not { } app) return;
        foreach (var source in registered) app.Resources["UiText:" + source] = Translate(source);
    }

    public static void Bind(Control control, AvaloniaProperty property, string source)
    {
        if (Avalonia.Application.Current is not { } app) return;
        registered.Add(source);
        var key = "UiText:" + source;
        app.Resources[key] = Translate(source);
        control.Bind(property, new DynamicResourceExtension(key));
    }
}
