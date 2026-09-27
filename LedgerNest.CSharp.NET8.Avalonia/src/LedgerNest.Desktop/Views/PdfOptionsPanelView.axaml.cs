using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfOptionsPanelView : UserControl
{
    public PdfOptionsPanelView() => InitializeComponent();

    public PdfOptionsPanelView(Control options) : this() => OptionsHost.Content = options;
}
