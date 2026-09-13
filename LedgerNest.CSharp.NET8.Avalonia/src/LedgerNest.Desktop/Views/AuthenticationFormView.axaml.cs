using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class AuthenticationFormView : UserControl
{
    public AuthenticationFormView()
    {
        InitializeComponent();
    }

    public AuthenticationFormView(string title, string subtitle, Control body)
    {
        InitializeComponent();
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        BodyHost.Content = body;
    }
}
