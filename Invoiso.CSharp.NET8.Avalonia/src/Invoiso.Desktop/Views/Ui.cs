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

namespace Invoiso.Desktop.Views;

internal static class Ui
{
    public static TextBlock Icon(string name, double size = 20, IBrush? color = null) => new() { Text = Icons.GetValueOrDefault(name, "\ue88f"), FontFamily = new FontFamily("avares://Invoiso.Desktop/Assets#Material Icons"), FontSize = size, Foreground = color ?? Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["calendar_today"] = "\ue935",
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
        ["account_balance_wallet"] = "\ue850",
        ["check_circle"] = "\ue86c",
        ["save"] = "\ue161",
        ["visibility"] = "\ue8f4",
        ["print"] = "\ue8ad",
        ["picture_as_pdf"] = "\ue415",
        ["dark_mode"] = "\ue51c",
    };
    public static IBrush Primary => Brush.Parse("#002E78");
    public static IBrush CardSurface => Brush.Parse("#FFF7FF");
    public static IBrush MaterialPrimary => Brush.Parse("#6750A4");
    public static IBrush Muted => Brush.Parse("#666666");
    public static IBrush Outline => Brush.Parse("#E0E0E0");
    public static TextBlock Text(string text, double size = 14, bool bold = false, IBrush? color = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Foreground = color ?? Brushes.Black, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    public static StackPanel Stack(double spacing, params Control[] children)
    { var p = new StackPanel { Spacing = spacing }; foreach (var c in children) p.Children.Add(c); return p; }
    public static WrapPanel Wrap(params Control[] children)
    { var p = new WrapPanel(); foreach (var c in children) { c.Margin = new Thickness(0, 0, 8, 8); p.Children.Add(c); } return p; }
    public static Grid Columns(string definitions, params Control[] children)
    { var g = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) }; for (var i = 0; i < children.Length; i++) { Grid.SetColumn(children[i], i); g.Children.Add(children[i]); } return g; }
    public static Grid Rows(string definitions, params Control[] children)
    { var g = new Grid { RowDefinitions = new RowDefinitions(definitions) }; for (var i = 0; i < children.Length; i++) { Grid.SetRow(children[i], i); g.Children.Add(children[i]); } return g; }
    public static Border Card(Control content, double padding = 16) => new() { Background = CardSurface, BorderBrush = Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = content };
    public static ScrollViewer Scroll(Control child, double padding = 16) => new() { Content = new Border { Padding = new Thickness(padding), Child = child }, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public static Button Button(string label, Action? action = null, bool primary = false)
    {
        var button = new Button { Content = label, Tag = label, Command = action == null ? null : new RelayCommand(action), IsEnabled = action != null, VerticalAlignment = VerticalAlignment.Center };
        var symbols = new Dictionary<string, string> { ["＋"] = "add", ["↑"] = "upload", ["↓"] = "download", ["↻"] = "refresh", ["×"] = "close", ["‹"] = "chevron_left", ["›"] = "chevron_right", ["⋯"] = "more_horiz", ["⇥"] = "logout" };
        if (label.Length > 0 && symbols.TryGetValue(label[..1], out var symbol))
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(Icon(symbol, 18, primary ? Brushes.White : Primary));
            if (label.Length > 1) content.Children.Add(Text(label[1..].Trim(), 14, color: primary ? Brushes.White : Primary));
            button.Content = content;
        }
        button.Classes.Add(primary ? "primary" : "outline");
        AutomationProperties.SetName(button, label);
        if (action == null) ToolTip.SetTip(button, "This service has not yet been migrated.");
        return button;
    }
    public static Control Field(FormField field, string? labelText = null, bool singleLine = false)
    {
        var withIcon = labelText == null && field.Icon.Length > 0;
        labelText ??= field.Label;
        Control input;
        var binding = new Binding(nameof(FormField.Value)) { Source = field, Mode = BindingMode.TwoWay };
        switch (field.Kind)
        {
            case "toggle":
                var thumb = new Avalonia.Controls.Shapes.Ellipse { Width = 16, Height = 16, Fill = Brushes.White, Margin = new Thickness(3) };
                var track = new Border { Width = 40, Height = 24, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Child = thumb };
                var toggle = new ToggleButton { Content = track, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
                toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FormField.IsChecked)) { Source = field, Mode = BindingMode.TwoWay });
                void PaintToggle() { track.Background = toggle.IsChecked == true ? Brush.Parse("#8097BD") : Brushes.White; track.BorderBrush = toggle.IsChecked == true ? Brushes.Transparent : Brush.Parse("#BDBDBD"); thumb.Fill = toggle.IsChecked == true ? Primary : Brush.Parse("#BDBDBD"); thumb.HorizontalAlignment = toggle.IsChecked == true ? HorizontalAlignment.Right : HorizontalAlignment.Left; }
                toggle.IsCheckedChanged += (_, _) => PaintToggle(); PaintToggle();
                AutomationProperties.SetName(toggle, field.Label);
                var caption = Stack(3, Text(labelText), Text(field.Help, 12, color: Muted)); if (field.Help.Length == 0) caption.Children[1].IsVisible = false;
                return Columns("*,12,Auto", caption, new Border(), toggle);
            case "choice":
                var combo = new ComboBox { ItemsSource = field.Options, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 44 };
                combo.Bind(SelectingItemsControl.SelectedItemProperty, binding); input = combo; break;
            case "date":
                var dateText = new TextBox { IsReadOnly = true, Watermark = labelText, MinHeight = 48, Padding = new Thickness(12, 12, 44, 12) };
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
                var selected = Text(field.Value.Length == 0 ? "No image selected" : field.Value, 12, color: Muted);
                var browse = Button("Upload image", () => { });
                browse.Click += async (_, _) =>
                {
                    if (TopLevel.GetTopLevel(browse) is not { } top) return;
                    var files = await top.StorageProvider.OpenFilePickerAsync(new() { Title = field.Label, AllowMultiple = false, FileTypeFilter = [Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll] });
                    if (files.Count > 0) { field.Value = files[0].Path.LocalPath; selected.Text = files[0].Name; }
                };
                input = Wrap(browse, selected, Button("Remove", () => { field.Value = ""; selected.Text = "No image selected"; })); break;
            default:
                var box = new TextBox { Watermark = labelText, MinHeight = 48, MaxLength = field.MaxLength, AcceptsReturn = field.Kind == "multiline" && !singleLine, TextWrapping = TextWrapping.Wrap, PasswordChar = field.Kind == "password" ? '●' : '\0' };
                if (field.Kind == "multiline" && !singleLine) box.MinHeight = 104;
                box.Bind(TextBox.TextProperty, binding);
                if (withIcon)
                {
                    box.MinHeight = field.Kind == "multiline" ? 104 : 56; box.Padding = new Thickness(50, 14, 12, 14); box.FontSize = 16;
                    var container = new Grid(); container.Children.Add(box);
                    var icon = Icon(field.Icon, 24); icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new Thickness(14, 0, 0, 0); icon.IsHitTestVisible = false; container.Children.Add(icon); input = container;
                }
                else input = box;
                break;
        }
        AutomationProperties.SetName(input, field.Label);
        var error = Text("", 12, color: Brushes.Firebrick);
        error.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Error)) { Source = field });
        var errors = new Binding(nameof(FormField.Error)) { Source = field, Converter = new Avalonia.Data.Converters.FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s)) };
        error.Bind(Visual.IsVisibleProperty, errors);
        if (field.Kind is "file" or "slider") return Stack(5, Text(labelText, 12, color: Muted), input, error);
        var floatLabel = new Border { Background = CardSurface, Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(9, -7, 0, 0), Child = Text(labelText + (field.Required && !labelText.EndsWith("*") ? " *" : ""), 12, color: Muted), IsHitTestVisible = false };
        var fieldGrid = new Grid(); input.Margin = new Thickness(0); fieldGrid.Children.Add(input); fieldGrid.Children.Add(floatLabel);
        void UpdateLabel() => floatLabel.IsVisible = field.Value.Length > 0 || input.IsKeyboardFocusWithin || field.Kind is "choice" or "date";
        input.GotFocus += (_, _) => UpdateLabel(); input.LostFocus += (_, _) => UpdateLabel();
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == nameof(FormField.Value)) UpdateLabel(); };
        fieldGrid.AttachedToVisualTree += (_, _) => field.PropertyChanged += changed;
        fieldGrid.DetachedFromVisualTree += (_, _) => field.PropertyChanged -= changed;
        UpdateLabel();
        return Stack(4, fieldGrid, error);
    }
    public static Control Segments(FormField field)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<Button>();
        void Update() { foreach (var b in buttons) b.Background = b.Tag?.ToString() == field.Value ? Brush.Parse("#E8DEF8") : Brushes.Transparent; }
        foreach (var option in field.Options)
        {
            var b = Button(option, () => { field.Value = option; Update(); }); b.Padding = new Thickness(14, 8); b.MinHeight = 36; b.CornerRadius = new CornerRadius(0); buttons.Add(b); panel.Children.Add(b);
        }
        Update(); return new Border { CornerRadius = new CornerRadius(20), ClipToBounds = true, Child = panel };
    }
    public static Control Fields(IEnumerable<FormField> fields, int columns = 1)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns))) };
        var array = fields.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            if (i % columns == 0) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var control = Field(array[i]); control.Margin = new Thickness(0, 0, i % columns < columns - 1 ? 12 : 0, i / columns < (array.Length - 1) / columns ? 16 : 0);
            Grid.SetColumn(control, i % columns); Grid.SetRow(control, i / columns); g.Children.Add(control);
        }
        return g;
    }
    public static Image Logo(bool compact = false) => Asset(compact ? "logo_v.png" : "logo.png", compact ? 38 : 154, 36);
    public static Image Asset(string name, double width, double height)
    { using var stream = AssetLoader.Open(new Uri($"avares://Invoiso.Desktop/Assets/{name}")); return new Image { Source = new Bitmap(stream), Width = width, Height = height, Stretch = Stretch.Uniform }; }
    public static Control Empty(string title, string subtitle = "", string icon = "▤")
    { var p = Stack(12, Icon(icon == "✓" ? "check_circle" : icon == "cart" ? "shopping_cart" : title.Contains("customers") ? "person_off" : "receipt_long", icon == "cart" ? 48 : 64, Outline), Text(title, 18, color: Muted), Text(subtitle, 14, color: Muted)); p.HorizontalAlignment = HorizontalAlignment.Center; p.VerticalAlignment = VerticalAlignment.Center; foreach (var c in p.Children) c.HorizontalAlignment = HorizontalAlignment.Center; return new Border { MinHeight = 240, Padding = new Thickness(24), Child = p }; }
    public static Control Header(string title, string subtitle, params Control[] actions)
    { var a = Wrap(actions); a.HorizontalAlignment = HorizontalAlignment.Right; return Columns("*,Auto", Stack(2, Text(title, 22, true), Text(subtitle, 13, color: Muted)), a); }
    public static Control AppBar(string title, params Control[] actions)
    {
        var a = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        foreach (var action in actions) a.Children.Add(action);
        var heading = Text(title, 20, color: Brushes.White); heading.TextWrapping = TextWrapping.NoWrap; heading.TextTrimming = TextTrimming.CharacterEllipsis;
        return new Border { Background = Primary, Padding = new Thickness(20, 0), Height = 56, Child = Columns("*,Auto", heading, a) };
    }
    public static Control Stats(params (string Label, string Value, string Subtitle, string Color)[] stats)
    {
        var grid = new Grid();
        void Arrange(double width)
        {
            var count = Math.Clamp((int)((width + 12) / 182), 1, stats.Length);
            grid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", count)));
            grid.RowDefinitions.Clear(); for (var i = 0; i < (stats.Length + count - 1) / count; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < grid.Children.Count; i++) { Grid.SetColumn(grid.Children[i], i % count); Grid.SetRow(grid.Children[i], i / count); }
        }
        foreach (var s in stats)
        {
            var icon = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.Parse(s.Color), .12), Opacity = 1, Child = Icon(s.Label.Contains("Customer") ? "people" : s.Label.Contains("Product") ? "inventory_2" : "receipt_long", 20, Brush.Parse(s.Color)) };
            var card = Card(Columns("*,Auto", Stack(6, Text(s.Label, 12, color: Muted), Text(s.Value, 24, true), Text(s.Subtitle, 11.5, color: Muted)), icon));
            card.Margin = new Thickness(0, 0, 12, 12); grid.Children.Add(card);
        }
        Arrange(1200); grid.SizeChanged += (_, e) => Arrange(e.NewSize.Width); return grid;
    }
}
