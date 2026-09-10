using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace LedgerNest.Desktop.Views;

public partial class ProductItemDialogView : UserControl
{
    public ProductItemDialogView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) == null) return;
            QuantityInput.Focus();
            QuantityInput.SelectAll();
        });
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ProductItemDialogViewModel model) return;
        if (e.Key == Key.Escape) { model.CancelCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Enter && e.Source is TextBox) { model.AddCommand.Execute(null); e.Handled = true; }
    }
}
