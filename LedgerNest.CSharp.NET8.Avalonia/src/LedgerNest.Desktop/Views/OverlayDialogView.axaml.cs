using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace LedgerNest.Desktop.Views;

public sealed partial class OverlayDialogView : UserControl
{
    private readonly Action close;

    public OverlayDialogView()
    {
        InitializeComponent();
        close = () => { };
    }

    public OverlayDialogView(string title, Control content, Control footer, Control? headerAccessory, Action close, bool side, double width, double availableWidth, double availableHeight)
    {
        InitializeComponent();
        this.close = close;
        TitleText.Text = title;
        BodyHost.Content = content;
        FooterHost.Content = footer;
        HeaderAccessoryHost.Content = headerAccessory ?? new Border();

        Panel.Width = side ? (availableWidth < 750 ? availableWidth - 32 : Math.Clamp(availableWidth * .42, 520, 680)) : width;
        Panel.MaxWidth = Math.Max(280, availableWidth - 32);
        Panel.MaxHeight = Math.Max(320, availableHeight - 32);
        Panel.HorizontalAlignment = side ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        Panel.VerticalAlignment = side ? VerticalAlignment.Stretch : VerticalAlignment.Center;
    }

    private void OnClosePressed(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => close();

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) => close();
}
