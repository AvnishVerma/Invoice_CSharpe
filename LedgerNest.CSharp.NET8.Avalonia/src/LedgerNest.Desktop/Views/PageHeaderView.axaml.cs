using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class PageHeaderView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PageHeaderView, string>(nameof(Title), "");
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public PageHeaderView() => InitializeComponent();

    public PageHeaderView(string title, IEnumerable<Control> actions) : this()
    {
        Title = title;
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
