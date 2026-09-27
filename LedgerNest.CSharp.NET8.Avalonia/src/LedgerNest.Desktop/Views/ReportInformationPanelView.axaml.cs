using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Displays explanatory copy in the common report-card layout.</summary>
public sealed partial class ReportInformationPanelView : UserControl
{
    public ReportInformationPanelView()
    {
        InitializeComponent();
    }

    public ReportInformationPanelView(string title, IEnumerable<string> details)
        : this()
    {
        TitleText.Text = title;
        DetailsList.ItemsSource = details.ToArray();
    }
}
