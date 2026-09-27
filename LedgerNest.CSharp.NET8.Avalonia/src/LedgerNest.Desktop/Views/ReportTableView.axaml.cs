using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts dynamic report rows within a uniform XAML report-table layout.</summary>
public sealed partial class ReportTableView : UserControl
{
    public ReportTableView()
    {
        InitializeComponent();
    }

    public ReportTableView(string title, Control actions, Control rows, Control footer, bool wrapInCard)
        : this()
    {
        TitleText.Text = title;
        TitleText.IsVisible = !string.IsNullOrWhiteSpace(title);
        ActionsHost.Content = actions;
        RowsHost.Content = rows;
        FooterHost.Content = footer;
        if (!wrapInCard)
        {
            Card.Padding = new Thickness(0);
            Card.Background = null;
            Card.BorderThickness = new Thickness(0);
        }
    }
}
