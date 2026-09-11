using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    private readonly Action loginSucceeded;
    private readonly Action forgotPassword;

    public LoginViewModel(MainWindowViewModel model, string message, Action loginSucceeded, Action forgotPassword)
    {
        this.model = model;
        this.loginSucceeded = loginSucceeded;
        this.forgotPassword = forgotPassword;
        Message = message;
    }

    public FormField Username { get; } = new("Username") { Icon = "person" };
    public FormField Password { get; } = new("Password", kind: "password") { Icon = "lock" };
    public string Message { get; private set; }
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
        OnPropertyChanged(nameof(Message));
    }

    [RelayCommand]
    private void ForgotPassword() => forgotPassword();
}
