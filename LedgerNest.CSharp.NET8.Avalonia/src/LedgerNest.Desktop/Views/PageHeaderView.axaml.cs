using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class PageHeaderView : UserControl
{
    public PageHeaderView() => InitializeComponent();

    public PageHeaderView(string title, IEnumerable<Control> actions) : this()
    {
        UiLocalization.Bind(Heading, TextBlock.TextProperty, title);
        foreach (var action in actions)
        {
            if (action is Button button)
            {
                button.Foreground = Brushes.White;
                button.Background = Ui.HeaderBand;
                button.BorderThickness = new Thickness(0);
                if (button.Content is TextBlock caption) caption.Foreground = Brushes.White;
                if (button.Content is Panel content)
                    foreach (var label in content.Children.OfType<TextBlock>()) label.Foreground = Brushes.White;
            }
            Actions.Children.Add(action);
        }
    }
}
