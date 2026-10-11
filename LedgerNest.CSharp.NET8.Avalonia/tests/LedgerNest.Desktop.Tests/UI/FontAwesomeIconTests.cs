using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class FontAwesomeIconTests(HeadlessFixture headless)
{
    [Fact]
    public Task EverySemanticIconExistsInBundledFont() => headless.Run(() =>
    {
        var face = new Typeface(new FontFamily(FontAwesomeIcons.FontFamily)).GlyphTypeface;
        Assert.Contains("Font Awesome", face.FamilyName);
        Assert.True(FontAwesomeIcons.Catalog.Count >= 138);
        foreach (var pair in FontAwesomeIcons.Catalog)
            Assert.True(face.CharacterToGlyphMap.GetGlyph(char.ConvertToUtf32(pair.Value, 0)) != 0, pair.Key);
    });

    [Fact]
    public Task SharedAxamlIconUsesBundledGlyphAndRequestedSize() => headless.Run(() =>
    {
        var view = new FontAwesomeIconView { DataContext = new FontAwesomeIconModel("refresh", 18, Brushes.Blue) };
        var window = new Window { Content = view, Width = 100, Height = 100 };
        try
        {
            window.Show(); window.UpdateLayout();
            var text = Assert.Single(view.GetVisualDescendants().OfType<TextBlock>());
            Assert.Equal(FontAwesomeIcons.Get("refresh"), text.Text);
            Assert.Equal(18, text.FontSize);
            Assert.Contains("Font Awesome", text.FontFamily.Name);
            Assert.Equal(Brushes.Blue, text.Foreground);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task UnknownIconHasExplicitFallbackAndEmptyIconStaysEmpty() => headless.Run(() =>
    {
        Assert.Equal("", FontAwesomeIcons.Get(null));
        Assert.Equal("", FontAwesomeIcons.Get(""));
        Assert.Equal(FontAwesomeIcons.Get("help"), FontAwesomeIcons.Get("not-a-known-icon"));
        Assert.NotEqual(FontAwesomeIcons.Get("help"), FontAwesomeIcons.Get("refresh"));
    });
}
