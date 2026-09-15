using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfSettingsShellView : UserControl
{
    private readonly Control templates;
    private readonly Control settings;
    private readonly Control preview;
    private readonly Grid wideLayout;
    private bool? previousNarrow;

    // Performs the PDF settings shell view initialization action for this screen or workflow.
    public PdfSettingsShellView()
    {
        InitializeComponent();
        templates = new Border();
        settings = new Border();
        preview = new Border();
        wideLayout = new Grid();
    }

    // Performs the PDF settings shell content assignment action for this screen or workflow.
    public PdfSettingsShellView(Control header, Control templates, Control settings, Control preview)
    {
        InitializeComponent();
        HeaderHost.Content = header;
        this.templates = templates;
        this.settings = settings;
        this.preview = preview;
        wideLayout = new Grid
        {
            Margin = new Thickness(12),
            ColumnDefinitions = new ColumnDefinitions("260,12,320,12,*")
        };
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width < 900);
        ApplyLayout(false);
    }

    // Performs the responsive PDF settings layout action for this screen or workflow.
    private void ApplyLayout(bool narrow)
    {
        if (previousNarrow == narrow) return;
        previousNarrow = narrow;
        ResponsiveHost.Content = null;
        wideLayout.Children.Clear();
        foreach (var item in new[] { templates, settings, preview })
        {
            if (item.Parent is Panel parent) parent.Children.Remove(item);
        }

        if (narrow)
        {
            templates.Height = 400;
            settings.Height = 520;
            preview.Height = 520;
            ResponsiveHost.Content = new ScrollViewer
            {
                Margin = new Thickness(12),
                Content = new StackPanel { Spacing = 12, Children = { templates, settings, preview } }
            };
            return;
        }

        templates.Height = double.NaN;
        settings.Height = double.NaN;
        preview.Height = double.NaN;
        Grid.SetColumn(templates, 0);
        Grid.SetColumn(settings, 2);
        Grid.SetColumn(preview, 4);
        wideLayout.Children.Add(templates);
        wideLayout.Children.Add(settings);
        wideLayout.Children.Add(preview);
        ResponsiveHost.Content = wideLayout;
    }
}
