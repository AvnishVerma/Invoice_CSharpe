using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductEditorFormView : UserControl
{
    public ProductEditorFormView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as ProductEditorViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as ProductEditorViewModel)?.Detach();
    }
    public ProductEditorFormView(FormField[] fields, Func<string, bool> visible) : this()
        => DataContext = new ProductEditorViewModel(fields, visible);
}
