using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductInputView : UserControl
{
    public ProductInputView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as ProductInputViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as ProductInputViewModel)?.Detach();
    }
    private Button? CalendarTrigger => this.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "CalendarButton");
    private void OpenCalendar(object? sender, PointerPressedEventArgs args) { if (CalendarTrigger is { } button) button.Flyout?.ShowAt(button); }
    private void CalendarKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Enter or Key.Space)) return;
        if (CalendarTrigger is { } button) button.Flyout?.ShowAt(button);
        args.Handled = true;
    }
    private void DateSelected(object? sender, SelectionChangedEventArgs args) => CalendarTrigger?.Flyout?.Hide();
    private void ClearDate(object? sender, RoutedEventArgs args)
    {
        if (DataContext is ProductInputViewModel model) model.DateAdapter.SelectedDate = null;
        CalendarTrigger?.Flyout?.Hide();
    }
}
