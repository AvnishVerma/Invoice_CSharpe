using System.Globalization;
using System.Text.Json;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace LedgerNest.Desktop;

/// <summary>Semantic icon data backed by the bundled Font Awesome Free 7.2.0 solid font.</summary>
public static class FontAwesomeIcons
{
    public const string FontFamily = "avares://LedgerNest.Desktop/Assets#Font Awesome 7 Free Solid";
    private static readonly Lazy<IReadOnlyDictionary<string, string>> catalog = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://LedgerNest.Desktop/Assets/FontAwesome-Icons.json"));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!
            .ToDictionary(pair => pair.Key, pair => char.ConvertFromUtf32(int.Parse(pair.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
    });
    public static IReadOnlyDictionary<string, string> Catalog => catalog.Value;
    public static string Get(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (Catalog.TryGetValue(name, out var glyph)) return glyph;
        if (name.Length == 1 && char.GetUnicodeCategory(name[0]) == UnicodeCategory.PrivateUse) return name;
        return Catalog["help"];
    }
}

public sealed class FontAwesomeGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => FontAwesomeIcons.Get(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class FontAwesomeGlyphExtension : MarkupExtension
{
    public string Name { get; set; } = "";
    public FontAwesomeGlyphExtension() { }
    public FontAwesomeGlyphExtension(string name) => Name = name;
    public override object ProvideValue(IServiceProvider serviceProvider) => FontAwesomeIcons.Get(Name);
}
