using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace LedgerNest.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            UsernameInput.Focus();
            UsernameInput.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not LoginViewModel model)
            return;

        e.Handled = true;
        model.LoginCommand.Execute(null);
    }
}
