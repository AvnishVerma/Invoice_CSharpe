using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

/// <summary>Displays a consistently styled report warning or informational notice.</summary>
public sealed partial class ReportNoticeView : UserControl
{
    public ReportNoticeView()
    {
        InitializeComponent();
    }

    public ReportNoticeView(string icon, string message, IBrush foreground, IBrush border)
        : this()
    {
        IconText.Text = icon;
        IconText.Foreground = foreground;
        MessageText.Text = message;
        MessageText.Foreground = foreground;
        Frame.BorderBrush = border;
    }
}
