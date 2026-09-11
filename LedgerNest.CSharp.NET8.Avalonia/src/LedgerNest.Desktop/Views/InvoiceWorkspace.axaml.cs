using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Resizable customer/items and invoice/options panes, stacked on smaller screens.</summary>
public sealed partial class InvoiceWorkspace : UserControl
{
    private readonly Control? left;
    private readonly Control? right;
    private readonly Control? items;
    private readonly Control? options;
    private bool? previousNarrow;

    public InvoiceWorkspace()
    {
        InitializeComponent();
        ConfigureColumns();
    }

    public InvoiceWorkspace(Control left, Control right, Control items, Control options)
    {
        InitializeComponent();
        this.left = left;
        this.right = right;
        this.items = items;
        this.options = options;

        ConfigureColumns();
        SizeChanged += (_, e) => ArrangeForWidth(e.NewSize.Width);
        AttachedToVisualTree += (_, _) => ArrangeForWidth(Bounds.Width);
    }

    private void ConfigureColumns()
    {
        WideRoot.ColumnDefinitions[0].MinWidth = 550;
        WideRoot.ColumnDefinitions[2].MinWidth = 300;
        WideRoot.ColumnDefinitions[2].MaxWidth = 550;
    }

    private void ArrangeForWidth(double width)
    {
        if (left == null || right == null || items == null || options == null)
            return;

        var narrow = width < 1000;
        if (previousNarrow == narrow)
            return;

        previousNarrow = narrow;
        WideLeft.Content = null;
        WideRight.Content = null;
        NarrowLeft.Content = null;
        NarrowRight.Content = null;

        items.MinHeight = narrow ? 360 : 0;
        options.MinHeight = narrow ? 540 : 0;
        WideRoot.IsVisible = !narrow;
        NarrowRoot.IsVisible = narrow;

        if (narrow)
        {
            WideRoot.Children.Remove(Splitter);
            NarrowLeft.Content = left;
            NarrowRight.Content = right;
            return;
        }

        if (!WideRoot.Children.Contains(Splitter))
        {
            Grid.SetColumn(Splitter, 1);
            WideRoot.Children.Add(Splitter);
        }
        WideLeft.Content = left;
        WideRight.Content = right;
    }
}
