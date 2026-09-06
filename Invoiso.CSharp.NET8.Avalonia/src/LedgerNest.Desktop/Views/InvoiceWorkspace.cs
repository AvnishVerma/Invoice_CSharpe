using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

/// <summary>Resizable customer/items and invoice/options panes, stacked on smaller screens.</summary>
public sealed class InvoiceWorkspace : ContentControl
{
    public InvoiceWorkspace(Control left, Control right, Control items, Control options)
    {
        Margin = new Thickness(10);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,360") };
        grid.ColumnDefinitions[0].MinWidth = 550;
        grid.ColumnDefinitions[2].MinWidth = 300;
        grid.ColumnDefinitions[2].MaxWidth = 550;
        var splitter = new GridSplitter
        {
            Width = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Background = Brush.Parse("#E0F2F1")
        };
        ToolTip.SetTip(splitter, "Drag to resize invoice panels");
        Avalonia.Automation.AutomationProperties.SetName(splitter, "Resize invoice panels");
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(right, 2);
        bool? previousNarrow = null;
        SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 1000;
            if (previousNarrow == narrow) return;
            previousNarrow = narrow;
            Content = null;
            if (left.Parent is Panel leftParent) leftParent.Children.Remove(left);
            if (right.Parent is Panel rightParent) rightParent.Children.Remove(right);
            grid.Children.Clear();
            items.MinHeight = narrow ? 360 : 0;
            options.MinHeight = narrow ? 540 : 0;
            if (narrow) Content = Ui.Scroll(Ui.Stack(8, left, right), 0);
            else
            {
                grid.Children.Add(left);
                grid.Children.Add(splitter);
                grid.Children.Add(right);
                Content = grid;
            }
        };
    }
}
