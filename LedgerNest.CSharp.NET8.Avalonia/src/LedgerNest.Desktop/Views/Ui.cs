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
    // Performs the icon action for this screen or workflow.
    public static TextBlock Icon(string name, double size = 20, IBrush? color = null) => new() { Text = Icons.GetValueOrDefault(name, "\ue88f"), FontFamily = new FontFamily("avares://LedgerNest.Desktop/Assets#Material Icons"), FontSize = size * .8, Foreground = color ?? Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["straighten"] = "\ue41c",
        ["factory"] = "\uebbc",
        ["expand_more"] = "\ue5cf",
        ["expand_less"] = "\ue5ce",
        ["local_shipping"] = "\ue558",
        ["lightbulb"] = "\ue0f0",
        ["description"] = "\ue873",
        ["notes"] = "\ue8ef",
        ["category"] = "\ue574",
        ["qr_code_2"] = "\ue00a",
        ["calendar_today"] = "\ue935",
        ["image"] = "\ue3f4",
        ["percent"] = "\ueb58",
        ["view_list"] = "\ue8ef",
        ["pin"] = "\uf045",
        ["tag"] = "\ue9ef",
        ["confirmation_number"] = "\ue638",
        ["favorite_border"] = "\ue87e",
        ["open_in_full"] = "\uf1ce",
        ["translate"] = "\ue8e2",
        ["label"] = "\ue892",
        ["content_copy"] = "\ue14d",
        ["badge"] = "\uea67",
        ["format_list_numbered"] = "\ue242",
        ["discount"] = "\uebc9",
        ["arrow_upward"] = "\ue5d8",
        ["arrow_downward"] = "\ue5db",
        ["lock"] = "\ue897",
        ["phone"] = "\ue0cd",
        ["email"] = "\ue0be",
        ["location_on"] = "\ue0c8",
        ["shopping_cart"] = "\ue8cc",
        ["person_off"] = "\ue510",
        ["dashboard"] = "\ue871",
        ["receipt"] = "\ue8b0",
        ["receipt_long"] = "\uef6e",
        ["request_quote"] = "\uf1b6",
        ["point_of_sale"] = "\uf17e",
        ["people"] = "\ue7fb",
        ["inventory_2"] = "\ue1a1",
        ["bar_chart"] = "\ue26b",
        ["settings"] = "\ue8b8",
        ["person"] = "\ue7fd",
        ["logout"] = "\ue9ba",
        ["keyboard"] = "\ue312",
        ["chevron_left"] = "\ue5cb",
        ["chevron_right"] = "\ue5cc",
        ["add"] = "\ue145",
        ["search"] = "\ue8b6",
        ["close"] = "\ue5cd",
        ["edit"] = "\ue3c9",
        ["delete"] = "\ue872",
        ["block"] = "\ue14b",
        ["download"] = "\uf090",
        ["upload"] = "\uf09b",
        ["business"] = "\ue0af",
        ["account_tree"] = "\ue97a",
        ["credit_card"] = "\ue870",
        ["account_balance"] = "\ue84f",
        ["build"] = "\ue869",
        ["check"] = "\ue5ca",
        ["backup"] = "\ue864",
        ["view_column"] = "\ue8ec",
        ["tune"] = "\ue429",
        ["accessibility_new"] = "\ue92c",
        ["info_outline"] = "\ue88f",
        ["refresh"] = "\ue5d5",
        ["more_horiz"] = "\ue5d3",
        ["groups"] = "\uf233",
        ["apartment"] = "\uea40",
        ["hourglass_top"] = "\uea5b",
        ["schedule"] = "\ue8b5",
        ["trending_up"] = "\ue8e5",
        ["savings"] = "\ue2eb",
        ["payments"] = "\uef63",
        ["warning_amber"] = "\uf083",
        ["account_balance_wallet"] = "\ue850",
        ["check_circle"] = "\ue86c",
        ["save"] = "\ue161",
        ["folder"] = "\ue2c7",
        ["upload_file"] = "\ue9fc",
        ["location_on"] = "\ue0c8",
        ["visibility"] = "\ue8f4",
        ["print"] = "\ue8ad",
        ["picture_as_pdf"] = "\ue415",
        ["dark_mode"] = "\ue51c",
    };
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
