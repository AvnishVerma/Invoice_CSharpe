using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ExpandedTextView : UserControl
{
    public ExpandedTextView() => InitializeComponent();
    private void ResizeEditor(object? sender, Avalonia.Input.VectorEventArgs args)
    {
        if (DataContext is ExpandedTextViewModel model) model.EditorHeight = Math.Clamp(model.EditorHeight + args.Vector.Y, 180, 600);
    }
}
