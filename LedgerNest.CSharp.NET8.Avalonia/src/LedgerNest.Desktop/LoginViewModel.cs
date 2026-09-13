using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    private readonly Action loginSucceeded;
    private readonly Action forgotPassword;
    private readonly Action<string> showAlert;

    public LoginViewModel(MainWindowViewModel model, string message, Action loginSucceeded, Action forgotPassword, Action<string> showAlert)
    {
        this.model = model;
        this.loginSucceeded = loginSucceeded;
        this.forgotPassword = forgotPassword;
        this.showAlert = showAlert;
        Message = message;
    }

    public FormField Username { get; } = new("Username") { Icon = "person" };
    public FormField Password { get; } = new("Password", kind: "password") { Icon = "lock" };
    public string Message { get; private set; }
    public string DisplayMessage => string.IsNullOrWhiteSpace(Message) ? " " : Message;
    public string Tagline => Branding.Tagline;
    public string FirstTimeHelp => "First time here? Log in with username admin and password admin, then set your own password when prompted.";

    [RelayCommand]
    public void Login()
    {
        if (model.SignIn(Username.Value, Password.Value))
        {
            loginSucceeded();
            return;
        }

        Message = model.Status;
        showAlert(Message);
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(DisplayMessage));
    }

    [RelayCommand]
    private void ForgotPassword() => forgotPassword();
}
