using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.LicenseManager;
using LedgerNest.LicensePublisher;
using System.Security.Cryptography;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        assertions++;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.Combine(Path.GetTempPath(), "ledgernest-publisher-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "ledgernest-publisher-screenshots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            AppBuilder.Configure<PublisherApp>().UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            var files = new TestFiles { Folder = root };
            var model = new PublisherViewModel(files);
            var window = new PublisherWindow { DataContext = model };
            window.Show();
            void Run(Task task)
            {
                var deadline = DateTime.UtcNow.AddSeconds(45);
                while (!task.IsCompleted && DateTime.UtcNow < deadline)
                { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
                Check(task.IsCompleted, "Publisher action timed out");
                task.GetAwaiter().GetResult();
                Dispatcher.UIThread.RunJobs();
            }
            void Capture(string name)
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame();
                Check(frame != null, "Publisher window did not render");
                frame!.Save(Path.Combine(output, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            Check(window.FindControl<Button>("IssueButton")?.Command != null, "Issue button command must be bound");
            window.FindControl<TextBox>("CustomerName")!.Text = "Acme Trading Ltd";
            window.FindControl<TextBox>("DeviceId")!.Text = new string('a', 64);
            Dispatcher.UIThread.RunJobs();
            Check(model.Customer == "Acme Trading Ltd" && model.DeviceId == new string('a', 64), "UI fields must update the view model");
            Capture("license-manager-issue");
            window.FindControl<TabControl>("Sections")!.SelectedIndex = 1;
            Capture("license-manager-keys");
            window.FindControl<TabControl>("Sections")!.SelectedIndex = 0;

            model.NewKeyPassword = "too short"; model.ConfirmPassword = "too short";
            Run(model.GenerateKeysCommand.ExecuteAsync(null));
            Check(!File.Exists(Path.Combine(root, "issuer.private.pem")) && model.Status.Contains("16 characters"), "Short passwords must not generate keys");
            Check(model.NewKeyPassword == "" && model.ConfirmPassword == "", "New-key passwords must clear after failure");
            var password = "Publisher test password " + Guid.NewGuid().ToString("N");
            model.NewKeyPassword = password; model.ConfirmPassword = password + "different";
            Run(model.GenerateKeysCommand.ExecuteAsync(null));
            Check(model.Status.Contains("do not match"), "Password confirmation must be checked");
            model.NewKeyPassword = password; model.ConfirmPassword = password;
            Run(model.GenerateKeysCommand.ExecuteAsync(null));
            Check(File.ReadAllText(model.PrivateKeyPath).Contains("BEGIN ENCRYPTED PRIVATE KEY"), "Private key must be encrypted");
            var publicKey = File.ReadAllText(Path.Combine(root, "public-key.pem"));
            var verifier = new LicenseVerifier(publicKey);
            Check(verifier.IsConfigured && model.NewKeyPassword == "" && !model.IsBusy, "Key generation must finish and clear passwords");
            var originalKey = File.ReadAllText(model.PrivateKeyPath);
            model.NewKeyPassword = password; model.ConfirmPassword = password;
            Run(model.GenerateKeysCommand.ExecuteAsync(null));
            Check(File.ReadAllText(model.PrivateKeyPath) == originalKey && model.Status.Contains("already contains"), "Generating keys must preserve existing key files");

            files.LicensePath = Path.Combine(root, "paid.ledgerlicense");
            model.Password = "wrong password";
            Run(model.IssueCommand.ExecuteAsync(null));
            Check(!File.Exists(files.LicensePath) && model.Status.Contains("Unable to unlock") && model.Password == "", "Wrong password must not issue a license and must clear");
            model.Password = password;
            Run(model.IssueCommand.ExecuteAsync(null));
            var document = File.ReadAllText(files.LicensePath);
            var status = verifier.Verify(document, model.DeviceId.ToUpperInvariant(), DateTimeOffset.UtcNow);
            Check(status.State == LicenseState.Active && status.Claims?.Customer == model.Customer, "GUI-issued license must verify in the customer application");
            Check(status.Claims!.ExpiresAtUtc - status.Claims.IssuedAtUtc == TimeSpan.FromDays(365), "Paid duration must be exact");
            Check(status.Allows(LicenseFeatures.BusinessWrite) && model.Password == "", "Issued license must carry the entitlement and clear the signing password");
            Check(verifier.Verify(document, new string('B', 64), DateTimeOffset.UtcNow).State == LicenseState.WrongDevice, "Issued license must reject other devices");
            Capture("license-manager-issued");
            model.Password = password;
            Run(model.IssueCommand.ExecuteAsync(null));
            Check(File.ReadAllText(files.LicensePath) == document && model.Status.StartsWith("Could not"), "Issuing must not overwrite an existing license");

            model.LicenseKindIndex = 1;
            Check(model.DurationDays == 30 && model.MaximumDays == 30, "Trial selection must adjust the duration");
            files.LicensePath = Path.Combine(root, "trial.ledgerlicense");
            model.DurationDays = 31; model.Password = password;
            Run(model.IssueCommand.ExecuteAsync(null));
            Check(!File.Exists(files.LicensePath), "Overlong trials must be rejected");
            model.DurationDays = 7; model.Password = password;
            Run(model.IssueCommand.ExecuteAsync(null));
            status = verifier.Verify(File.ReadAllText(files.LicensePath), model.DeviceId.ToUpperInvariant(), DateTimeOffset.UtcNow);
            Check(status.State == LicenseState.Trial && status.Claims!.ExpiresAtUtc - status.Claims.IssuedAtUtc == TimeSpan.FromDays(7), "Trial issuance must verify with the selected duration");
            model.LicenseKindIndex = 2; model.Password = password;
            files.LicensePath = Path.Combine(root, "perpetual.ledgerlicense");
            Run(model.IssueCommand.ExecuteAsync(null));
            status = verifier.Verify(File.ReadAllText(files.LicensePath), model.DeviceId.ToUpperInvariant(), DateTimeOffset.UtcNow);
            Check(!model.HasDuration && status.State == LicenseState.Active && status.Claims!.ExpiresAtUtc == null, "Perpetual paid licenses must not expire");

            model.DeviceId = "invalid"; model.Password = password;
            files.LicensePath = Path.Combine(root, "invalid.ledgerlicense");
            Run(model.IssueCommand.ExecuteAsync(null));
            Check(!File.Exists(files.LicensePath) && model.Status.Contains("64-character"), "Invalid device IDs must be rejected");
            model.DeviceId = new string('A', 64); model.Customer = " "; model.Password = password;
            Run(model.IssueCommand.ExecuteAsync(null));
            Check(!File.Exists(files.LicensePath) && model.Status.Contains("customer name"), "Empty customer names must be rejected");
            model.Customer = "Acme Trading Ltd"; model.Password = password; files.LicensePath = null;
            Run(model.IssueCommand.ExecuteAsync(null));
            Check(model.Status.Contains("cancelled") && model.Password == "", "Cancelled save must not issue a license and must clear the password");

            model.Password = password; files.PublicPath = Path.Combine(root, "recovered-public.pem");
            Run(model.ExportPublicKeyCommand.ExecuteAsync(null));
            Check(File.ReadAllText(files.PublicPath) == publicKey && model.Password == "", "Public-key recovery must match the existing private key");
            model.Password = password; model.NewKeyPassword = password; model.ConfirmPassword = password;
            window.Close();
            Check(model.Password == "" && model.NewKeyPassword == "" && model.ConfirmPassword == "", "Closing the publisher must clear passwords");
            Console.WriteLine($"License Manager checks passed: {assertions} assertions. Screenshots: {output}");
        }
        finally
        {
            // This directory is created uniquely by this test and contains only disposable test keys.
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestFiles : IPublisherFiles
    {
        public string? Folder { get; set; }
        public string? LicensePath { get; set; }
        public string? PublicPath { get; set; }
        public Task<string?> OpenPrivateKeyAsync() => Task.FromResult<string?>(null);
        public Task<string?> SelectKeyFolderAsync() => Task.FromResult(Folder);
        public Task<string?> SaveLicenseAsync() => Task.FromResult(LicensePath);
        public Task<string?> SavePublicKeyAsync() => Task.FromResult(PublicPath);
    }
}
