using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Invoiso.Desktop.Views;

namespace Invoiso.Desktop;

public partial class MainWindow
{
    internal void CloseOverlay() { overlay.Children.Clear(); overlay.IsVisible = false; }
    internal void ShowOverlay(string title, Control content, Control? footer = null, bool side = false, double width = 560, Control? headerAccessory = null)
    {
        overlay.Children.Clear(); overlay.IsVisible = true;
        overlay.Margin = new Thickness(side ? (Model.SidebarExpanded ? 210 : 64) : 0, 0, 0, 0);
        var scrim = new Border { Background = Brush.Parse("#4D000000") }; scrim.PointerPressed += (_, _) => CloseOverlay(); overlay.Children.Add(scrim);
        var panel = Ui.Card(Ui.Rows("Auto,*,Auto", new Border { Padding = new Thickness(18, 16), Child = Ui.Columns("*,Auto,Auto", Ui.Text(title, 16, true), headerAccessory ?? new Border(), Ui.Button("×", CloseOverlay)) }, Ui.Scroll(content, 18), new Border { Padding = new Thickness(18), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1, 0, 0), Child = footer ?? Ui.Button("Close", CloseOverlay) }), 0);
        var availableWidth = Bounds.Width - overlay.Margin.Left;
        panel.Width = side ? (availableWidth < 750 ? availableWidth - 32 : Math.Clamp(availableWidth * .42, 520, 680)) : width; panel.MaxWidth = Math.Max(280, availableWidth - 32); panel.MaxHeight = Math.Max(320, Bounds.Height - 32); panel.Margin = new Thickness(16);
        panel.HorizontalAlignment = side ? HorizontalAlignment.Right : HorizontalAlignment.Center; panel.VerticalAlignment = side ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        overlay.Children.Add(panel);
    }
    internal void Confirm(string title, string message, Action action)
    { ShowOverlay(title, Ui.Text(message), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Confirm", () => { action(); CloseOverlay(); }, true)), width: 460); }
}
