using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private void ShowLogin()
    {
        var username = new FormField("Username") { Icon = "person" }; var password = new FormField("Password", kind: "password") { Icon = "lock" };
        var logo = Ui.Logo();
        var login = Ui.Button("Login"); login.HorizontalAlignment = HorizontalAlignment.Stretch; login.Height = 50; login.CornerRadius = new CornerRadius(25);
        var forgot = Ui.Button("Forgot password?", ShowForgotPassword); forgot.Classes.Add("text"); forgot.HorizontalAlignment = HorizontalAlignment.Right;
        var content = Ui.Stack(16, logo, Ui.Card(Ui.Text("First time here? Log in with username admin and password admin, then set your own password when prompted.", 13, color: Ui.Muted), 12), Ui.Field(username), Ui.Field(password), new Border { Height = 0 }, login, forgot, new TextBlock { Text = Branding.Tagline, FontSize = 12, Foreground = Ui.Muted, HorizontalAlignment = HorizontalAlignment.Center });
        overlay.Margin = new Thickness(0); overlay.Children.Clear(); overlay.IsVisible = true;
        var card = Ui.Card(content, 32); card.Width = 420; card.MaxWidth = Math.Max(280, Bounds.Width - 48); card.Background = Brush.Parse("#FAFAFA"); card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center; card.BoxShadow = BoxShadows.Parse("0 6 16 0 #33000000");
        overlay.Children.Add(new Border { Background = Brush.Parse("#E3F2FD"), Child = Ui.Scroll(card, 24) });
    }
    private void ShowForgotPassword()
    {
        var username = new FormField("Username", required: true);
        var response = new FormField("Response Code", required: true);
        var password = new FormField("New Password (min 8 characters)", kind: "password", required: true);
        var confirm = new FormField("Confirm New Password", kind: "password", required: true);
        ShowOverlay("Reset Password", Ui.Stack(18, Ui.Text("Recover access to your account", 22, true), Ui.Text("Enter your username to start password recovery.", 13, color: Ui.Muted), Ui.Field(username), Ui.Button("Generate Challenge"), Ui.Text("Challenge Code", 13, true), new TextBox { IsReadOnly = true, Watermark = "Challenge code" }, Ui.Field(response), Ui.Field(password), Ui.Field(confirm)), Ui.Wrap(Ui.Button("Back to Login", ShowLogin), Ui.Button("Reset Password")), width: 520);
    }
    private void ShowChangePassword()
    {
        FormField[] fields = [new("Current Password", kind: "password", required: true), new("New Password (min 8 characters)", kind: "password", required: true), new("Confirm New Password", kind: "password", required: true)];
        ShowOverlay("Change Password", Ui.Stack(20, Ui.Text("Choose a strong password to secure your account.", 13, color: Ui.Muted), Ui.Fields(fields)), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Change Password")), width: 520);
    }
    private void ShowOnboarding()
    {
        var step = 0;
        var company = new FormField[] { new("Company Name", required: true), new("Country", "India", "choice", ["India", "Nepal", "United States", "United Kingdom"]), new("Company Logo", kind: "file") };
        var invoice = new FormField[] { new("Currency", "INR", "choice", ["INR", "USD", "EUR", "GBP", "NPR"]), new("Date Format", "dd/MM/yyyy", "choice", ["dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"]), new("Starting Number", "1", "number"), new("Leading Zeros", "true", "toggle"), new("Default Tax Rate (%)", "18", "number") };
        var appearance = new FormField[] { new("Page Size", "A4", "choice", ["A4", "A5", "Letter", "Thermal 80mm"]), new("Template", "Classic", "choice", ["Classic", "Modern", "Minimal", "Compact", "Executive", "Grid Classic", "Thermal"]) };
        void Render()
        {
            string[] names = ["Company", "Invoice", "Appearance", "You're all set!"];
            var content = Ui.Stack(24, Ui.Logo(), Ui.Wrap(Ui.Text("1  Company", 13, step == 0), Ui.Text("2  Invoice", 13, step == 1), Ui.Text("3  Appearance", 13, step == 2)), Ui.Text(names[step], 24, true));
            if (step < 3) content.Children.Add(Ui.Fields(step == 0 ? company : step == 1 ? invoice : appearance));
            else content.Children.Add(Ui.Empty("You're all set!", "Start creating invoices for your business.", "✓"));
            ShowOverlay($"Welcome to {Branding.Name}", content, Ui.Wrap(Ui.Button(step == 0 ? "Cancel" : "Back", () => { if (step == 0) CloseOverlay(); else { step--; Render(); } }), Ui.Button(step == 3 ? "Get Started" : "Continue", () =>
            {
                if (step == 3) { CloseOverlay(); return; }
                if (!(step == 0 ? company : step == 1 ? invoice : appearance).Select(f => f.Validate()).ToArray().All(v => v)) return;
                step++; Render();
            }, true)), width: 700);
        }
        Render();
    }
}
