using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementActionBarView : UserControl
{
    public ManagementActionBarView()
    {
        InitializeComponent();
    }

    public ManagementActionBarView(string title, params Control[] actions) : this()
    {
        TitleText.Text = title;
        foreach (var action in actions)
        {
            if (action is Button button && !button.Classes.Contains("primary"))
            {
                button.Classes.Remove("outline");
                button.Classes.Add("header-action");
                button.Foreground = Brushes.White;
                button.Background = Brushes.Transparent;
                button.BorderBrush = new SolidColorBrush(Colors.White, .65);
                button.BorderThickness = new Thickness(1);
                if (button.Content is TextBlock caption) caption.Foreground = Brushes.White;
                if (button.Content is StackPanel content)
                    foreach (var child in content.Children.OfType<TextBlock>()) child.Foreground = Brushes.White;
            }
            ActionsHost.Children.Add(action);
        }
    }
}
