using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private void ShowLogin() => ShowLogin("");

    private void ShowLogin(string message)
    {
        Model.CurrentOverlay = new LoginViewModel(Model, message, ContinueAfterLogin, ShowForgotPassword, alert => Model.Status = alert);
    }

    private void ContinueAfterLogin()
    {
        if (Model.RequiresPasswordChange) ShowChangePassword();
        else if (Model.NeedsFirstTimeSetup) ShowOnboarding();
        else CloseOverlay();
    }

    private void ShowForgotPassword()
    {
        FormField[] fields = [new("Username", required: true), new("Response Code", required: true),
            new("New Password (min 8 characters)", kind: "password", required: true),
            new("Confirm New Password", kind: "password", required: true)];
        var form = new AuthenticationFormViewModel("Recover access to your account",
            "Enter your username to start password recovery.", fields, recovery: true);
        ShowOverlay("Reset Password", form, new DialogActions([
            new DialogAction("Back to Login", new RelayCommand(ShowLogin)),
            new DialogAction("Reset Password", null)
        ]), width: 520);
    }

    private void ShowChangePassword()
    {
        if (Model.CurrentUsername == null) { ShowLogin(); return; }
        FormField[] fields = [new("Current Password", kind: "password", required: true),
            new("New Password (min 8 characters)", kind: "password", required: true),
            new("Confirm New Password", kind: "password", required: true)];
        ShowOverlay("Change Password", new AuthenticationFormViewModel("Change Password",
            "Choose a strong password to secure your account.", fields), new DialogActions([
            new DialogAction("Cancel", new RelayCommand(CloseOverlay)),
            new DialogAction("Change Password", new RelayCommand(() =>
            {
                if (Model.ChangeCurrentPassword(fields)) ContinueAfterLogin();
            }), true)
        ]), width: 520);
    }

    private void ShowOnboarding()
    {
        var onboarding = new OnboardingViewModel(Model, CloseOverlay);
        ShowOverlay($"Welcome to {Branding.Name}", onboarding, new OnboardingActions(onboarding), width: 700);
    }
}
