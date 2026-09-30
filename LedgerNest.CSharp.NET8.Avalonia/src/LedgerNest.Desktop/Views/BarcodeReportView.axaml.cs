using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class BarcodeReportView : UserControl
{
    public BarcodeReportView() => InitializeComponent();
    public BarcodeReportView(BarcodeReportViewModel model) : this() => DataContext = model;
}
