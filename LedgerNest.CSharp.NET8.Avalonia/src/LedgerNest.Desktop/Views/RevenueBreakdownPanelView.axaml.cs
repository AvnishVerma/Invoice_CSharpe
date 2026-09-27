using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the XAML frame for the monthly revenue-breakdown table.</summary>
public sealed partial class RevenueBreakdownPanelView : UserControl
{
    public RevenueBreakdownPanelView()
    {
        InitializeComponent();
    }

    public RevenueBreakdownPanelView(Control rows)
        : this()
    {
        RowsHost.Content = rows;
    }
}
