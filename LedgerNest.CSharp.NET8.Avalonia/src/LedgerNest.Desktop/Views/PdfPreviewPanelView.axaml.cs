using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfPreviewPanelView : UserControl
{
    public PdfPreviewPanelView() => InitializeComponent();

    public PdfPreviewPanelView(Control preview) : this() => PreviewHost.Content = preview;
}
