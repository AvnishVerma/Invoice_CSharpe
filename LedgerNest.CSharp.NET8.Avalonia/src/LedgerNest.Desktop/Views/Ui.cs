using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LedgerNest.Desktop.Views;

internal static class Ui
{
    public static IBrush HeaderBand => Brush.Parse(Branding.HeaderColor);
    // Legacy composition callers consume the same AXAML-defined icon visual as other views.
    public static Control Icon(string name, double size = 20, IBrush? color = null)
        => new FontAwesomeIconView { DataContext = new FontAwesomeIconModel(name, size, color ?? Muted) };
    private static readonly Dictionary<(string Light, string Dark), SolidColorBrush> palette = [];
    private static bool darkTheme;
    // Performs the palette action for this screen or workflow.
    public static IBrush Palette(string light, string dark)
    {
        var key = (light, dark);
        if (!palette.TryGetValue(key, out var brush))
            palette[key] = brush = new SolidColorBrush(Color.Parse(darkTheme ? dark : light));
        return brush;
    }
    // Performs the update theme action for this screen or workflow.
    public static void UpdateTheme(bool dark)
    {
        darkTheme = dark;
        foreach (var entry in palette)
            entry.Value.Color = Color.Parse(dark ? entry.Key.Dark : entry.Key.Light);
    }
    public static IBrush Accent => Palette(Branding.PrimaryColor, "#93C5FD");
    public static IBrush Surface => Palette("#FAFAFA", "#18212B");
    public static IBrush Canvas => Palette("#FFFFFF", "#111820");
    public static IBrush TextColor => Palette("#000000", "#F1F5F9");
    public static IBrush Primary => Palette(Branding.PrimaryColor, "#93C5FD");
    public static IBrush CardSurface => Palette("#F7FAFC", "#202B36");
    public static IBrush MaterialPrimary => Primary;
    public static IBrush Muted => Palette("#666666", "#BBC5D0");
    public static IBrush Outline => Palette("#E0E0E0", "#526171");
    // Performs the text action for this screen or workflow.
    public static TextBlock Text(string text, double size = 14, bool bold = false, IBrush? color = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Foreground = color ?? TextColor, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    // Performs the stack action for this screen or workflow.
    public static StackPanel Stack(double spacing, params Control[] children)
    { var p = new StackPanel { Spacing = spacing }; foreach (var c in children) p.Children.Add(c); return p; }
    // Performs the wrap action for this screen or workflow.
    public static WrapPanel Wrap(params Control[] children)
    { var p = new WrapPanel(); foreach (var c in children) { c.Margin = new Thickness(0, 0, 8, 8); p.Children.Add(c); } return p; }
    // Performs the columns action for this screen or workflow.
    public static Grid Columns(string definitions, params Control[] children)
    { var g = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) }; for (var i = 0; i < children.Length; i++) { Grid.SetColumn(children[i], i); g.Children.Add(children[i]); } return g; }
    // Performs the rows action for this screen or workflow.
    public static Grid Rows(string definitions, params Control[] children)
    { var g = new Grid { RowDefinitions = new RowDefinitions(definitions) }; for (var i = 0; i < children.Length; i++) { Grid.SetRow(children[i], i); g.Children.Add(children[i]); } return g; }
    // Performs the card action for this screen or workflow.
    public static Border Card(Control content, double padding = 16) => new() { Background = CardSurface, BorderBrush = Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = content };
    // Performs the scroll action for this screen or workflow.
    public static ScrollViewer Scroll(Control child, double padding = 16) => new() { Content = new Border { Padding = new Thickness(padding), Child = child }, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    // Performs the load logo action for this screen or workflow.
    public static Avalonia.Media.Imaging.Bitmap? LoadLogo(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (!value.StartsWith("base64:", StringComparison.Ordinal)) return new Avalonia.Media.Imaging.Bitmap(value);
            using var stream = new MemoryStream(Convert.FromBase64String(value[7..]));
            return new Avalonia.Media.Imaging.Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or FormatException) { return null; }
    }

    public static TextBlock LocalText(string text, double size = 14, bool bold = false, IBrush? color = null)
    {
        var block = Text(text, size, bold, color);
        UiLocalization.Bind(block, TextBlock.TextProperty, text);
        return block;
    }
    // Performs the button action for this screen or workflow.
    public static Button Button(string label, Action? action = null, bool primary = false) => new ActionButtonView(label, action, primary);

    // Legacy callers load an AXAML field view; control structure and bindings live in that view.
    public static Control Field(FormField field, string? labelText = null, bool singleLine = false, bool compact = true)
        => new FormFieldView { DataContext = new FormFieldViewModel(field, labelText, singleLine, compact) };
    // Performs the segments action for this screen or workflow.
    public static Control Segments(FormField field) => new SegmentedChoiceView { DataContext = new SegmentedChoiceViewModel(field) };
    public static Control Fields(IEnumerable<FormField> fields, int columns = 1, bool compact = true)
        => new FormFieldsView { DataContext = new FormFieldsViewModel(fields.Select(field => new FormFieldViewModel(field, compact: compact)).ToArray(), columns) };
    // Performs the logo action for this screen or workflow.
    public static Control Logo(bool compact = false) => new BrandLogo(compact);
    // Performs the asset action for this screen or workflow.
    public static Image Asset(string name, double width, double height)
    { return new Image { Source = AssetBitmap(name), Width = width, Height = height, Stretch = Stretch.Uniform }; }
    // Performs the asset bitmap load action for reusable image-backed controls.
    public static Bitmap AssetBitmap(string name)
    { using var stream = AssetLoader.Open(new Uri($"avares://LedgerNest.Desktop/Assets/{name}")); return new Bitmap(stream); }
    // Performs the empty action for this screen or workflow.
    public static Control Empty(string title, string subtitle = "", string icon = "▤")
        => new EmptyStateView { DataContext = new EmptyStateViewModel(title, subtitle,
            icon == "✓" ? "check_circle" : icon == "cart" ? "shopping_cart" : title.Contains("customers") ? "person_off" : "receipt_long") };
    // Performs the header action for this screen or workflow.
    public static Control Header(string title, string subtitle, params Control[] actions)
    { var a = Wrap(actions); a.HorizontalAlignment = HorizontalAlignment.Right; return Columns("*,Auto", Stack(2, LocalText(title, 22, true), LocalText(subtitle, 13, color: Muted)), a); }
    // Performs the app bar action for this screen or workflow.
    public static Control AppBar(string title, params Control[] actions) => new PageHeaderView(title, actions);
    // Performs the stats action for this screen or workflow.
    public static Control Stats(params (string Label, string Value, string Subtitle, string Color)[] stats) => new StatisticsView(stats);
}
