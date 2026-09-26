using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Automation;
using CommunityToolkit.Mvvm.Input;

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
    public static IBrush Accent => Palette("#0F766E", "#5EEAD4");
    public static IBrush Surface => Palette("#FAFAFA", "#18212B");
    public static IBrush Canvas => Palette("#FFFFFF", "#111820");
    public static IBrush TextColor => Palette("#000000", "#F1F5F9");
    public static IBrush Primary => Palette(Branding.PrimaryColor, "#80CBC4");
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
    public static Button Button(string label, Action? action = null, bool primary = false)
    {
        var button = new Button { Content = label, Tag = label, Command = action == null ? null : new RelayCommand(action), IsEnabled = action != null, VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center };
        var caption = LocalText(label);
        caption.ClearValue(TextBlock.ForegroundProperty);
        button.Content = caption;
        var symbols = new Dictionary<string, string> { ["＋"] = "add", ["↑"] = "upload", ["↓"] = "download", ["↻"] = "refresh", ["×"] = "close", ["‹"] = "chevron_left", ["›"] = "chevron_right", ["⋯"] = "more_horiz", ["⇥"] = "logout" };
        if (label.Length > 0 && symbols.TryGetValue(label[..1], out var symbol))
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(Icon(symbol, 18, primary ? Brushes.White : Accent));
            if (label.Length > 1) content.Children.Add(LocalText(label[1..].Trim(), 14, color: primary ? Brushes.White : Accent));
            button.Content = content;
        }
        if (!primary) button.Foreground = Accent;
        button.Classes.Add(primary ? "primary" : "outline");
        AutomationProperties.SetName(button, label);
        if (action == null) ToolTip.SetTip(button, "This service has not yet been migrated.");
        return button;
    }
    // Performs the field action for this screen or workflow.
    public static Control Field(FormField field, string? labelText = null, bool singleLine = false, bool compact = true)
    {
        var withIcon = labelText == null && field.Icon.Length > 0;
        labelText ??= field.Label;
        Control input;
        var binding = new Binding(nameof(FormField.Value)) { Source = field, Mode = BindingMode.TwoWay };
        switch (field.Kind)
        {
            case "toggle":
                var thumb = new Avalonia.Controls.Shapes.Ellipse { Width = compact ? 13 : 16, Height = compact ? 13 : 16, Fill = Brushes.White, Margin = new Thickness(compact ? 2 : 3) };
                var track = new Border { Width = compact ? 32 : 40, Height = compact ? 20 : 24, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Child = thumb };
                var toggle = new ToggleButton { Content = track, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
                toggle.Classes.Add("form-toggle");
                toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FormField.IsChecked)) { Source = field, Mode = BindingMode.TwoWay });
                void PaintToggle() { track.Background = toggle.IsChecked == true ? Brush.Parse("#8097BD") : Brushes.White; track.BorderBrush = toggle.IsChecked == true ? Brushes.Transparent : Brush.Parse("#BDBDBD"); thumb.Fill = toggle.IsChecked == true ? Primary : Brush.Parse("#BDBDBD"); thumb.HorizontalAlignment = toggle.IsChecked == true ? HorizontalAlignment.Right : HorizontalAlignment.Left; }
                toggle.IsCheckedChanged += (_, _) => PaintToggle(); PaintToggle();
                AutomationProperties.SetName(toggle, field.Label);
                var caption = Stack(3, LocalText(labelText), LocalText(field.Help, 12, color: Muted)); if (field.Help.Length == 0) caption.Children[1].IsVisible = false;
                return Columns("*,12,Auto", caption, new Border(), toggle);
            case "choice":
                var combo = new ComboBox
                {
                    ItemsSource = field.Options,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MinHeight = compact ? 32 : 40,
                    Padding = new Thickness(10, compact ? 5 : 7),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                combo.Bind(SelectingItemsControl.SelectedItemProperty, binding); input = combo; break;
            case "date":
                var dateText = new TextBox
                {
                    IsReadOnly = true,
                    PlaceholderText = labelText,
                    MinHeight = compact ? 32 : 40,
                    Padding = new Thickness(10, compact ? 5 : 7, 36, compact ? 5 : 7),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                dateText.Bind(TextBox.TextProperty, new Binding(nameof(FormField.Value)) { Source = field, Converter = new Avalonia.Data.Converters.FuncValueConverter<string, string>(value => DateTime.TryParse(value, out var parsed) ? parsed.ToString("dd/MM/yyyy") : "") });
                var calendar = new Calendar { SelectedDate = DateTime.TryParse(field.Value, out var d) ? d : null, DisplayDate = DateTime.TryParse(field.Value, out var initial) ? initial : DateTime.Today };
                var flyout = new Flyout { Content = calendar };
                calendar.SelectedDatesChanged += (_, _) => { field.Value = calendar.SelectedDate?.ToString("yyyy-MM-dd") ?? ""; flyout.Hide(); };
                var dateButton = Button("", () => flyout.ShowAt(dateText)); dateButton.Content = Icon("calendar_today", 18); dateButton.Classes.Add("text"); dateButton.HorizontalAlignment = HorizontalAlignment.Right; dateButton.Margin = new Thickness(0, 0, 4, 0);
                dateText.PointerPressed += (_, _) => flyout.ShowAt(dateText);
                var dateGrid = new Grid(); dateGrid.Children.Add(dateText); dateGrid.Children.Add(dateButton); input = dateGrid; break;
            case "slider":
                var slider = new Slider { Minimum = 0, Maximum = 100, Value = (double)field.Number };
                slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) field.Value = slider.Value.ToString("0"); };
                input = slider; break;
            case "file":
                var selected = Text(field.Value.Length == 0 ? "No image selected" : "Image selected", 12, color: Muted);
                var browse = Button("Upload image", () => { });
                browse.Click += async (_, _) =>
                {
                    if (TopLevel.GetTopLevel(browse) is not { } top) return;
                    var files = await top.StorageProvider.OpenFilePickerAsync(new() { Title = field.Label, AllowMultiple = false, FileTypeFilter = [Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll] });
                    if (files.Count > 0)
                    {
                        try
                        {
                            await using var stream = await files[0].OpenReadAsync();
                            using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
                            if (bytes.Length > 2 * 1024 * 1024) { field.Error = "Logo must be 2 MB or smaller."; return; }
                            var value = "base64:" + Convert.ToBase64String(bytes.ToArray());
                            using var bitmap = LoadLogo(value);
                            if (bitmap == null || bitmap.PixelSize.Width > 1080 || bitmap.PixelSize.Height > 1080) { field.Error = "Choose an image up to 1080 × 1080 pixels."; return; }
                            field.Value = value; field.Error = ""; selected.Text = files[0].Name;
                        }
                        catch (IOException) { field.Error = "The image could not be read."; }
                    }
                };
                input = Wrap(browse, selected, Button("Remove", () => { field.Value = ""; selected.Text = "No image selected"; })); break;
            default:
                var box = new TextBox
                {
                    PlaceholderText = labelText,
                    MinHeight = compact ? 32 : 40,
                    MaxLength = field.MaxLength,
                    AcceptsReturn = field.Kind == "multiline" && !singleLine,
                    TextWrapping = TextWrapping.Wrap,
                    PasswordChar = field.Kind == "password" ? '●' : '\0',
                    Padding = new Thickness(10, compact ? 5 : 7),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                if (field.Kind == "multiline" && !singleLine) box.MinHeight = compact ? 84 : 104;
                box.Bind(TextBox.TextProperty, binding);
                if (withIcon)
                {
                    box.MinHeight = field.Kind == "multiline" ? (compact ? 84 : 104) : (compact ? 36 : 44);
                    box.Padding = new Thickness(compact ? 38 : 44, compact ? 5 : 7, 10, compact ? 5 : 7);
                    box.FontSize = compact ? 14 : 16;
                    var container = new Grid(); container.Children.Add(box);
                    var icon = Icon(field.Icon, compact ? 18 : 20); icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new Thickness(compact ? 10 : 12, 0, 0, 0); icon.IsHitTestVisible = false; container.Children.Add(icon); input = container;
                }
                else input = box;
                break;
        }
        if (compact)
        {
            void CompactInput(Control control)
            {
                if (control is TextBox text)
                {
                    text.MinHeight = Math.Min(text.MinHeight, text.AcceptsReturn ? 84 : 36);
                    var p = text.Padding == default ? new Thickness(10, 5) : text.Padding;
                    text.Padding = new Thickness(p.Left, Math.Min(p.Top, 5), p.Right, Math.Min(p.Bottom, 5));
                    text.VerticalContentAlignment = VerticalAlignment.Center;
                }
                else if (control is ComboBox choice)
                {
                    choice.MinHeight = Math.Min(choice.MinHeight, 32);
                    choice.Padding = new Thickness(10, 5);
                }
                if (control is Panel panel)
                    foreach (var child in panel.Children) CompactInput(child);
                if (control is Button button)
                {
                    button.MinHeight = 30;
                    button.Padding = new Thickness(8, 4);
                }
            }
            CompactInput(input);
        }
        if (input is TextBox textInput) UiLocalization.Bind(textInput, TextBox.PlaceholderTextProperty, labelText);
        else if (input is Grid inputGrid)
            foreach (var textInputChild in inputGrid.Children.OfType<TextBox>()) UiLocalization.Bind(textInputChild, TextBox.PlaceholderTextProperty, labelText);
        if (input is ComboBox choiceInput)
            choiceInput.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((value, _) => LocalText(value ?? ""));
        AutomationProperties.SetName(input, field.Label);
        var error = Text("", 12, color: Brushes.Firebrick);
        error.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Error)) { Source = field });
        var errors = new Binding(nameof(FormField.Error)) { Source = field, Converter = new Avalonia.Data.Converters.FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s)) };
        error.Bind(Visual.IsVisibleProperty, errors);
        if (field.Kind is "file" or "slider") return Stack(5, LocalText(labelText, 12, color: Muted), input, error);
        var floatLabel = new Border { Background = CardSurface, Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(9, -7, 0, 0), Child = LocalText(labelText + (field.Required && !labelText.EndsWith("*") ? " *" : ""), 12, color: Muted), IsHitTestVisible = false };
        var fieldGrid = new Grid(); input.Margin = new Thickness(0); fieldGrid.Children.Add(input); fieldGrid.Children.Add(floatLabel);
        void UpdateLabel() => floatLabel.IsVisible = field.Value?.Length > 0 || input.IsKeyboardFocusWithin || field.Kind is "choice" or "date";
        input.GotFocus += (_, _) => UpdateLabel(); input.LostFocus += (_, _) => UpdateLabel();
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == nameof(FormField.Value)) UpdateLabel(); };
        fieldGrid.AttachedToVisualTree += (_, _) => field.PropertyChanged += changed;
        fieldGrid.DetachedFromVisualTree += (_, _) => field.PropertyChanged -= changed;
        UpdateLabel();
        return Stack(4, fieldGrid, error);
    }
    // Performs the segments action for this screen or workflow.
    public static Control Segments(FormField field)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<Button>();
        void Update() { foreach (var b in buttons) b.Background = b.Tag?.ToString() == field.Value ? Palette("#E8DEF8", "#3A4C69") : Brushes.Transparent; }
        foreach (var option in field.Options)
        {
            var b = Button(option, () => { field.Value = option; Update(); }); b.Classes.Add("segment"); buttons.Add(b); panel.Children.Add(b);
        }
        Update(); return new Border { CornerRadius = new CornerRadius(20), ClipToBounds = true, Child = panel };
    }
    // Performs the fields action for this screen or workflow.
    public static Control Fields(IEnumerable<FormField> fields, int columns = 1, bool compact = true)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns))) };
        var array = fields.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            if (i % columns == 0) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var control = Field(array[i], compact: compact); control.Margin = new Thickness(0, 0, i % columns < columns - 1 ? 12 : 0, i / columns < (array.Length - 1) / columns ? 16 : 0);
            Grid.SetColumn(control, i % columns); Grid.SetRow(control, i / columns); g.Children.Add(control);
        }
        return g;
    }
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
    { var p = Stack(12, Icon(icon == "✓" ? "check_circle" : icon == "cart" ? "shopping_cart" : title.Contains("customers") ? "person_off" : "receipt_long", icon == "cart" ? 48 : 64, Outline), LocalText(title, 18, color: Muted), LocalText(subtitle, 14, color: Muted)); p.HorizontalAlignment = HorizontalAlignment.Center; p.VerticalAlignment = VerticalAlignment.Center; foreach (var c in p.Children) c.HorizontalAlignment = HorizontalAlignment.Center; return new Border { MinHeight = 240, Padding = new Thickness(24), Child = p }; }
    // Performs the header action for this screen or workflow.
    public static Control Header(string title, string subtitle, params Control[] actions)
    { var a = Wrap(actions); a.HorizontalAlignment = HorizontalAlignment.Right; return Columns("*,Auto", Stack(2, LocalText(title, 22, true), LocalText(subtitle, 13, color: Muted)), a); }
    // Performs the app bar action for this screen or workflow.
    public static Control AppBar(string title, params Control[] actions) => new PageHeaderView(title, actions);
    // Performs the stats action for this screen or workflow.
    public static Control Stats(params (string Label, string Value, string Subtitle, string Color)[] stats) => new StatisticsView(stats);
}
