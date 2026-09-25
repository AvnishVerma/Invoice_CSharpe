using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

internal sealed class ProductEditorFormView : UserControl
{
    internal static IBrush Surface => Ui.Palette("#FFF8FF", "#282330");
    internal static IBrush Purple => Brush.Parse("#6750A4");

    public ProductEditorFormView(FormField[] fields, Func<string, bool> visible)
    {
        FormField F(string label) => fields.Single(f => f.Label == label);
        Control Input(string label, string icon = "") => ProductInput(F(label), icon);
        var body = Ui.Stack(24);
        void Section(string title, params (string Label, Control Control)[] rows)
        {
            var shown = rows.Where(row => visible(row.Label)).Select(row => row.Control).ToArray();
            if (shown.Length > 0) body.Children.Add(Ui.Stack(9.6, Ui.LocalText(title, 11, true, Ui.Muted), Ui.Stack(12.8, shown)));
        }
        Control Pair(string first, Control left, string second, Control right)
        {
            if (!visible(first)) return visible(second) ? right : new Border();
            return !visible(second) ? left : Ui.Columns("*,12,*", left, new Border(), right);
        }

        var aliasHelp = Ui.Icon("info_outline", 18, Brush.Parse("#6366F1"));
        aliasHelp.HorizontalAlignment = HorizontalAlignment.Left; aliasHelp.Margin = new Thickness(16, 0, 0, 0);
        ToolTip.SetTip(aliasHelp, "Local-language name used when Show Alias Name in PDF is enabled in Invoice Settings.");
        Section("GENERAL", ("Name", Input("Name", "inventory_2")),
            ("Alias Name", Ui.Stack(4.8, Input("Alias Name (for invoice PDF)", "translate"), aliasHelp)),
            ("Description", Input("Description", "description")), ("HSN/SAC", Input("HSN/SAC", "qr_code_2")));

        Section("PRICING", ("Price", Pair("Sale Price", Input("Sale Price"), "Purchase Price", Input("Purchase Price"))),
            ("Default Discount", Input("Default Discount")),
            ("Tax Rate", Ui.Stack(1.6, Pair("Tax (%)", Input("Tax (%)", "percent"), "Price includes tax", Check(F("Price includes tax"))),
                Ui.LocalText("Per-item tax mode only", 12, color: Ui.Muted))));

        var stock = Input("Stock", "inventory_2");
        var customUnit = Input("Custom unit", "straighten");
        var unlimited = F("Unlimited stock"); var unit = F("Unit");
        void UpdateDependencies()
        {
            stock.IsEnabled = !unlimited.IsChecked;
            customUnit.IsVisible = visible("Unit") && unit.Value == "Custom…";
        }
        System.ComponentModel.PropertyChangedEventHandler dependencyChanged = (_, _) => UpdateDependencies();
        AttachedToVisualTree += (_, _) => { unlimited.PropertyChanged += dependencyChanged; unit.PropertyChanged += dependencyChanged; };
        DetachedFromVisualTree += (_, _) => { unlimited.PropertyChanged -= dependencyChanged; unit.PropertyChanged -= dependencyChanged; };
        UpdateDependencies();
        Section("INVENTORY", (visible("Stock") ? "Stock" : "Unit", Pair("Stock", stock, "Unit", Input("Unit", "straighten"))),
            ("Unit", customUnit), ("Stock", Check(unlimited, "Track infinite stock for this product")));

        var metadata = Ui.Stack(16);
        foreach (var (label, icon) in new[] { ("Storage Location", "location_on"), ("Container Number", "inventory_2"),
            ("Batch Number", "tag"), ("Expiry Date", "calendar_today"), ("Manufacture Date", "calendar_today"),
            ("Manufacturer Name", "factory"), ("Supplier Name", "local_shipping"), ("SKU Code", "qr_code_2"), ("Notes", "notes") })
            if (visible(label)) metadata.Children.Add(Input(label, icon));
        if (metadata.Children.Count > 0)
        {
            var header = Ui.Columns("24,12,*", Ui.Icon("more_horiz", 22, Purple), new Border(), Ui.LocalText("Advanced Information", 15));
            var expander = new Expander { HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, BorderThickness = new Thickness(0), Padding = new Thickness(0),
                Name = "ProductAdvancedInformation" };
            expander.Template = new Avalonia.Controls.Templates.FuncControlTemplate<Expander>((owner, _) =>
            {
                var arrow = Ui.Icon("expand_more", 22, Purple);
                var toggle = new ToggleButton { Content = Ui.Columns("*,Auto", header, arrow),
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0, 6.4), MinHeight = 35.2 };
                toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(Expander.IsExpanded)) { Source = owner, Mode = BindingMode.TwoWay });
                toggle.IsCheckedChanged += (_, _) => arrow.Text = toggle.IsChecked == true ? "\ue5ce" : "\ue5cf";
                AutomationProperties.SetName(toggle, "Advanced Information");
                var host = new ContentControl { Content = metadata, Margin = new Thickness(0, 8, 0, 0) };
                host.Bind(IsVisibleProperty, new Binding(nameof(Expander.IsExpanded)) { Source = owner });
                return Ui.Stack(6.4, toggle, host);
            });
            body.Children.Add(expander);
        }
        body.Children.Add(new Border { Background = Ui.Palette("#FFF5ED", "#3B3026"), BorderBrush = Brush.Parse("#FFD18B"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(9.6), Child = Ui.Columns("16,10,*", Ui.Icon("lightbulb", 16, Brush.Parse("#FFB300")), new Border(),
                Ui.LocalText("Tip: Choose optional product fields in Settings → Product Details.", 12, color: Ui.Muted)) });
        Content = body;
    }

    private static Control Check(FormField field, string help = "")
    {
        var check = new CheckBox { Content = help.Length == 0 ? Ui.Text(field.Label, 13) : Ui.Stack(2.4, Ui.Text(field.Label, 13), Ui.LocalText(help, 12, color: Ui.Muted)) };
        check.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FormField.IsChecked)) { Source = field, Mode = BindingMode.TwoWay });
        AutomationProperties.SetName(check, field.Label);
        return check;
    }

    internal static Control ProductInput(FormField field, string icon)
    {
        var binding = new Binding(nameof(FormField.Value)) { Source = field, Mode = BindingMode.TwoWay };
        Control input;
        if (field.Kind == "choice")
        {
            var combo = new ComboBox { ItemsSource = field.Options, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 38.4,
                Padding = new Thickness(icon.Length == 0 ? 10 : 34, 6, 10, 6), Background = Brushes.Transparent, CornerRadius = new CornerRadius(10) };
            combo.Bind(SelectingItemsControl.SelectedItemProperty, binding); input = combo;
        }
        else
        {
            var text = new TextBox { PlaceholderText = field.Label, MinHeight = field.Kind == "multiline" ? 76.8 : 38.4,
                AcceptsReturn = field.Kind == "multiline", TextWrapping = field.Kind == "multiline" ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Padding = new Thickness(icon.Length == 0 ? 10 : 34, field.Kind == "multiline" ? 20 : 8, 10, 8), FontSize = 15,
                Background = Brushes.Transparent, BorderBrush = Brush.Parse("#C8C3CA"), CornerRadius = new CornerRadius(10), MaxLength = field.MaxLength };
            if (field.Kind == "date")
            {
                text.IsReadOnly = true;
                text.Bind(TextBox.TextProperty, new Binding(nameof(FormField.Value)) { Source = field,
                    Converter = new FuncValueConverter<string, string>(v => DateTime.TryParse(v, out var date) ? date.ToString("dd/MM/yyyy") : "") });
                var calendar = new Calendar { SelectedDate = DateTime.TryParse(field.Value, out var date) ? date : null };
                var flyout = new Flyout();
                flyout.Content = Ui.Stack(6.4, calendar, Ui.Button("Clear date", () => { field.Value = ""; calendar.SelectedDate = null; flyout.Hide(); }));
                calendar.SelectedDatesChanged += (_, _) => { field.Value = calendar.SelectedDate?.ToString("yyyy-MM-dd") ?? ""; flyout.Hide(); };
                text.PointerPressed += (_, _) => flyout.ShowAt(text);
                text.KeyDown += (_, e) => { if (e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space) { flyout.ShowAt(text); e.Handled = true; } };
            }
            else text.Bind(TextBox.TextProperty, binding);
            input = text;
        }
        AutomationProperties.SetName(input, field.Label);
        var grid = new Grid(); grid.Children.Add(input);
        if (icon.Length > 0)
        {
            var glyph = Ui.Icon(icon, 23); glyph.HorizontalAlignment = HorizontalAlignment.Left;
            glyph.Margin = new Thickness(10, 0, 0, 0); glyph.IsHitTestVisible = false; grid.Children.Add(glyph);
        }
        var label = new Border { Background = Surface, Padding = new Thickness(3.2, 0), Margin = new Thickness(9, -7, 0, 0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false,
            Child = Ui.Text(field.Label, 12, color: Ui.Muted) };
        grid.Children.Add(label);
        void UpdateLabel() => label.IsVisible = field.Value.Length > 0 || input.IsKeyboardFocusWithin || field.Kind == "choice";
        input.GotFocus += (_, _) => UpdateLabel(); input.LostFocus += (_, _) => UpdateLabel();
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) =>
        {
            if (e.PropertyName == nameof(FormField.Value) && field.Error.Length > 0) field.Validate();
            UpdateLabel();
        };
        grid.AttachedToVisualTree += (_, _) => field.PropertyChanged += changed;
        grid.DetachedFromVisualTree += (_, _) => field.PropertyChanged -= changed;
        UpdateLabel();
        var error = Ui.LocalText("", 12, color: Brushes.Firebrick);
        error.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Error)) { Source = field });
        error.Bind(IsVisibleProperty, new Binding(nameof(FormField.Error)) { Source = field, Converter = new FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s)) });
        return Ui.Stack(3.2, grid, error);
    }

    internal static Control TypeSelector(FormField type)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<Button>();
        void Refresh()
        {
            foreach (var button in buttons)
            {
                var selected = type.Value == button.Tag?.ToString();
                button.Background = selected ? Ui.Palette("#E8DEF8", "#202B36") : Brushes.Transparent;
                button.Content = Ui.Text((selected ? "✓  " : "") + button.Tag, 13);
            }
        }
        foreach (var value in type.Options)
        {
            var button = Ui.Button(value, () => { type.Value = value; Refresh(); });
            button.Tag = value; button.Width = 104; button.Height = 25.6; button.MinHeight = 25.6;
            button.Padding = new Thickness(6.4, 3.2); button.CornerRadius = new CornerRadius(0);
            AutomationProperties.SetName(button, value);
            buttons.Add(button); panel.Children.Add(button);
        }
        Refresh();
        return new Border { Child = panel, BorderBrush = Brush.Parse("#C8C3CA"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), ClipToBounds = true };
    }
}
