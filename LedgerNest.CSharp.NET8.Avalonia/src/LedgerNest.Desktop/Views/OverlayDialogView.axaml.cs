using Avalonia.Controls;
using Avalonia.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class OverlayDialogView : UserControl
{
    public OverlayDialogView() => InitializeComponent();

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is OverlayDialogViewModel model) model.CloseCommand.Execute(null);
    }
}
