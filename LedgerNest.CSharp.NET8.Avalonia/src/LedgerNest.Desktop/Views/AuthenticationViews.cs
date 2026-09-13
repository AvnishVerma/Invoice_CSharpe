using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private void ShowLogin() => ShowLogin("");

    private void ShowLogin(string message)
    {
        overlay.Margin = new Avalonia.Thickness(0);
        overlay.Children.Clear();
        overlay.IsVisible = true;
        overlay.Children.Add(new LoginView { DataContext = new LoginViewModel(Model, message, ContinueAfterLogin, ShowForgotPassword, alert => Model.Status = alert) });
    }
    private void ContinueAfterLogin()
    {
        if (Model.RequiresPasswordChange) ShowChangePassword();
        else if (Model.NeedsFirstTimeSetup) ShowOnboarding();
        else CloseOverlay();
    }

    private void ShowForgotPassword()
    {
        var username = new FormField("Username", required: true);
        var response = new FormField("Response Code", required: true);
        var password = new FormField("New Password (min 8 characters)", kind: "password", required: true);
        var confirm = new FormField("Confirm New Password", kind: "password", required: true);
        var body = Ui.Stack(18, Ui.Field(username), Ui.Button("Generate Challenge"), Ui.Text("Challenge Code", 13, true), new TextBox { IsReadOnly = true, PlaceholderText = "Challenge code" }, Ui.Field(response), Ui.Field(password), Ui.Field(confirm));
        ShowOverlay("Reset Password", new AuthenticationFormView("Recover access to your account", "Enter your username to start password recovery.", body), Ui.Wrap(Ui.Button("Back to Login", ShowLogin), Ui.Button("Reset Password")), width: 520);
    }
    private void ShowChangePassword()
    {
        if (Model.CurrentUsername == null) { ShowLogin(); return; }
        FormField[] fields = [new("Current Password", kind: "password", required: true), new("New Password (min 8 characters)", kind: "password", required: true), new("Confirm New Password", kind: "password", required: true)];
        ShowOverlay("Change Password", new AuthenticationFormView("Change Password", "Choose a strong password to secure your account.", Ui.Fields(fields)), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Change Password", () => { if (Model.ChangeCurrentPassword(fields)) ContinueAfterLogin(); }, true)), width: 520);
    }
    private void ShowOnboarding()
    {
        var step = 0;
        var groups = Model.CreateOnboardingFields();
        var company = groups[0];
        var invoice = groups[1];
        var appearance = groups[2];
        void Render()
        {
            string[] names = ["Company", "Invoice", "Appearance", "You're all set!"];
            var body = step < 3 ? Ui.Fields(step == 0 ? company : step == 1 ? invoice : appearance) : Ui.Empty("You're all set!", "Start creating invoices for your business.", "✓");
            var content = new OnboardingStepView(names[step], Ui.Wrap(Ui.Text("1  Company", 13, step == 0), Ui.Text("2  Invoice", 13, step == 1), Ui.Text("3  Appearance", 13, step == 2)), body);
            ShowOverlay($"Welcome to {Branding.Name}", content, Ui.Wrap(Ui.Button(step == 0 ? "Cancel" : "Back", () => { if (step == 0) CloseOverlay(); else { step--; Render(); } }), Ui.Button(step == 3 ? "Get Started" : "Continue", () =>
            {
                if (step == 3) { if (Model.CompleteOnboarding(groups)) CloseOverlay(); else { step = company.Any(f => f.Error.Length > 0) ? 0 : 1; Render(); } return; }
                if (!(step == 0 ? company : step == 1 ? invoice : appearance).Select(f => f.Validate()).ToArray().All(v => v)) return;
                step++; Render();
            }, true)), width: 700);
        }
        Render();
    }
}
