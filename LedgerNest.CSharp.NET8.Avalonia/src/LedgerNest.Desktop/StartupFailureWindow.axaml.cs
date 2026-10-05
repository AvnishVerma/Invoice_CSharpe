using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class StartupFailureWindow : Window
{
    public StartupFailureWindow() => InitializeComponent();
}

public sealed record StartupFailureViewModel
{
    public string Message { get; }
    public RelayCommand RetryCommand { get; }
    public RelayCommand CloseCommand { get; }
    public StartupFailureViewModel(string message, Action retry, Action close)
    { Message = message; RetryCommand = new(retry); CloseCommand = new(close); }
}
