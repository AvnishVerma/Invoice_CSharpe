using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfSettingsShellView : UserControl
{
    private readonly Control templates;
    private readonly Control settings;
    private readonly Control preview;
    private bool? previousNarrow;

    // Performs the PDF settings shell view initialization action for this screen or workflow.
    public PdfSettingsShellView()
    {
        InitializeComponent();
        templates = new Border();
        settings = new Border();
        preview = new Border();
    }

    // Performs the PDF settings shell content assignment action for this screen or workflow.
    public PdfSettingsShellView(Control header, Control templates, Control settings, Control preview)
    {
        InitializeComponent();
        HeaderHost.Content = header;
        this.templates = templates;
        this.settings = settings;
        this.preview = preview;
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width < 900);
        ApplyLayout(false);
    }

    // Performs the responsive PDF settings layout action for this screen or workflow.
    private void ApplyLayout(bool narrow)
    {
        if (previousNarrow == narrow) return;
        previousNarrow = narrow;
        WideTemplatesHost.Content = null;
        WideSettingsHost.Content = null;
        WidePreviewHost.Content = null;
        NarrowTemplatesHost.Content = null;
        NarrowSettingsHost.Content = null;
        NarrowPreviewHost.Content = null;

        if (narrow)
        {
            templates.Height = 400;
            settings.Height = 520;
            preview.Height = 520;
            WideLayout.IsVisible = false;
            NarrowLayout.IsVisible = true;
            NarrowTemplatesHost.Content = templates;
            NarrowSettingsHost.Content = settings;
            NarrowPreviewHost.Content = preview;
            return;
        }

        templates.Height = double.NaN;
        settings.Height = double.NaN;
        preview.Height = double.NaN;
        NarrowLayout.IsVisible = false;
        WideLayout.IsVisible = true;
        WideTemplatesHost.Content = templates;
        WideSettingsHost.Content = settings;
        WidePreviewHost.Content = preview;
    }
}
