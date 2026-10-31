using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

/// <summary>Supplies label-based hints without replacing explicit help or exposing input values.</summary>
public sealed class UiTooltips : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<UiTooltips, Control, bool>("Enabled");
    private static readonly AttachedProperty<string?> GeneratedProperty =
        AvaloniaProperty.RegisterAttached<UiTooltips, Control, string?>("Generated");
    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);
    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);

    static UiTooltips() => EnabledProperty.Changed.AddClassHandler<Control>((control, _) =>
    {
        control.PointerEntered -= Entered;
        control.AttachedToVisualTree -= Attached;
        control.PropertyChanged -= Changed;
        if (!GetEnabled(control)) return;
        control.PointerEntered += Entered;
        control.AttachedToVisualTree += Attached;
        control.PropertyChanged += Changed;
        Refresh(control);
    });

    private static void Entered(object? sender, PointerEventArgs args) { if (sender is Control control) Refresh(control); }
    private static void Attached(object? sender, VisualTreeAttachmentEventArgs args) { if (sender is Control control) Refresh(control); }
    private static void Changed(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is Control control && args.Property.Name is "Name" or "Content" or "Header" or "PlaceholderText" or "Caption" or "ActionLabel" or "Tooltip")
            Refresh(control);
    }

    internal static void Refresh(Control control)
    {
        var current = ToolTip.GetTip(control);
        var generated = control.GetValue(GeneratedProperty);
        if (current is not null && current is not string) return;
        if (current is string explicitText && !string.IsNullOrWhiteSpace(explicitText) && explicitText != generated) return;
        // Template editors and buttons are described by their owning input, not its current value.
        if (control.TemplatedParent is TextBox or NumericUpDown or CalendarDatePicker or ComboBox or DatePicker) return;
        var label = control is ActionButtonView action
            ? First(action.Tooltip, action.Caption, action.ActionLabel)
            : AutomationProperties.GetName(control);
        if (string.IsNullOrWhiteSpace(label))
            label = control switch
            {
                TextBox text => text.PlaceholderText,
                ComboBox choice => choice.PlaceholderText,
                HeaderedContentControl header => ContentText(header.Header),
                HeaderedSelectingItemsControl header => ContentText(header.Header),
                ContentControl content => ContentText(content.Content),
                _ => null
            };
        if (string.IsNullOrWhiteSpace(label) && control.Tag is string tag) label = tag;
        if (string.IsNullOrWhiteSpace(label) && control.Parent is Panel panel)
        {
            var index = panel.Children.IndexOf(control);
            if (index > 0 && panel.Children[index - 1] is TextBlock heading) label = heading.Text;
        }
        if (string.IsNullOrWhiteSpace(label)) return;
        var translated = UiLocalization.Translate(label);
        control.SetValue(GeneratedProperty, translated);
        control.SetCurrentValue(ToolTip.TipProperty, translated);
    }

    private static string? First(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    private static string? ContentText(object? content) => content switch
    {
        string text => text,
        TextBlock text when !text.Classes.Contains("action-icon-glyph") && text.Text?.Any(char.IsLetterOrDigit) == true => text.Text,
        Control container => string.Join(" ", container.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => !text.Classes.Contains("action-icon-glyph") && text.Text?.Any(char.IsLetterOrDigit) == true)
            .Select(text => text.Text)),
        _ => null
    };
}
