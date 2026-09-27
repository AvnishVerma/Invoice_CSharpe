using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfTemplateSelectorView : UserControl
{
    public PdfTemplateSelectorView() => InitializeComponent();

    public PdfTemplateSelectorView(Control pageSize, Control templates) : this()
    {
        PageSizeHost.Content = pageSize;
        TemplatesHost.Content = templates;
    }
}
