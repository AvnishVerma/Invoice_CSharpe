using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductTypeSelectorView : UserControl
{
    public ProductTypeSelectorView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as SegmentedChoiceViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as SegmentedChoiceViewModel)?.Detach();
    }
}
