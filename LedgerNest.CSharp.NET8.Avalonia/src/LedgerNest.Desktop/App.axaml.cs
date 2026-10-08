using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LedgerNest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using LedgerNest.Desktop.Printing;
using LedgerNest.Desktop.Notifications;
using LedgerNest.Application;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? serviceProvider;
    private bool exitHandlerAttached;
    public App()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            AppErrorLog.Write(e.Exception, "Unhandled UI exception");
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.DataContext: MainWindowViewModel vm })
            {
                vm.Status = $"An error occurred. Details saved to {AppErrorLog.Path}";
                e.Handled = true;
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                AppErrorLog.Write(exception, "Unhandled application exception");
        };
    }

    // Performs the initialize action for this screen or workflow.
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    // Performs first-run database selection before application services and business data are created.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LedgerNest");
            Directory.CreateDirectory(dataDirectory);
            var databasePath = Path.Combine(dataDirectory, "ledgernest.db");
            var profilePath = Path.Combine(dataDirectory, "database-profile.json");
            // An established registry is authoritative even when the old setup profile is missing.
            var profile = File.Exists(Path.Combine(dataDirectory, "companies.json"))
                ? DatabaseProfile.LocalSqlite(databasePath) : DatabaseProfileStore.Load(profilePath);
            if (profile == null && File.Exists(databasePath))
            {
                profile = DatabaseProfile.LocalSqlite(databasePath);
                DatabaseProfileStore.Save(profilePath, profile);
            }
            if (profile == null)
            {
                desktop.MainWindow = new DatabaseSetupWindow(databasePath, selected =>
                {
                    DatabaseProfileStore.Save(profilePath, selected);
                    StartDesktop(desktop, selected, dataDirectory);
                });
            }
            else StartDesktop(desktop, profile, dataDirectory);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartDesktop(IClassicDesktopStyleApplicationLifetime desktop, DatabaseProfile profile, string dataDirectory)
    {
        var registry = new CompanyRegistryService(dataDirectory);
        try { OpenCompany(desktop, registry, registry.Initialize(profile).ActiveCompanyId, dataDirectory); }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, "Opening company workspace");
            var failure = new StartupFailureViewModel("LedgerNest could not open the company workspace. " + ex.Message,
                () => StartDesktop(desktop, profile, dataDirectory), () => desktop.Shutdown());
            var previous = desktop.MainWindow;
            desktop.MainWindow = new StartupFailureWindow { DataContext = failure };
            desktop.MainWindow.Show();
            previous?.Close();
        }
    }

    private void OpenCompany(IClassicDesktopStyleApplicationLifetime desktop, CompanyRegistryService registry, string companyId, string dataDirectory)
    {
        var company = registry.Read().Companies.SingleOrDefault(item => item.Id == companyId)
            ?? throw new InvalidOperationException("The configured workspace is unavailable. Check database configuration.");
        var profile = company.Database;
        var nextProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddInfrastructure(profile)
            .AddSingleton<IPdfGenerator, TemporaryPdfGenerator>()
            .AddSingleton<IPrintServiceFactory, PrintServiceFactory>()
            .AddSingleton<IToastService, AvaloniaToastService>()
            .AddSingleton<ILicenseService>(_ => CreateLicenseService(dataDirectory))
            .BuildServiceProvider();

        try
        {
            var companyContext = new CompanyWorkspaceContext(registry, company, selected =>
            {
                OpenCompany(desktop, registry, selected.Id, dataDirectory);
            });
            var databasePath = profile.Provider == DatabaseProvider.Sqlite
                ? new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(profile.ConnectionString).DataSource : null;
            var nextModel = new MainWindowViewModel(nextProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<LedgerNestDbContext>>(),
                databasePath, nextProvider.GetRequiredService<ILicenseService>(), companyContext);
            var printService = nextProvider.GetRequiredService<IPrintServiceFactory>().Create();
            var nextWindow = new MainWindow(printService, nextProvider.GetRequiredService<IPdfGenerator>(), nextProvider.GetRequiredService<IToastService>())
            { DataContext = nextModel };
            var previousWindow = desktop.MainWindow;
            var previousProvider = serviceProvider;
            try
            {
                DatabaseProfileStore.Save(Path.Combine(dataDirectory, "database-profile.json"), profile);
                CompanySessionTransition.Switch(registry, previousWindow?.DataContext as MainWindowViewModel, nextModel, () =>
                {
                    desktop.MainWindow = nextWindow;
                    nextWindow.Show();
                });
                serviceProvider = nextProvider;
            }
            catch
            {
                desktop.MainWindow = previousWindow;
                nextWindow.Close();
                throw;
            }
            try { previousWindow?.Close(); previousProvider?.Dispose(); }
            catch (Exception ex)
            {
                AppErrorLog.Write(ex, "Cleaning up the previous company workspace");
                nextModel.Status = "The database workspace opened, but the previous workspace could not finish cleanup. Restart the application.";
            }
        }
        catch { nextProvider.Dispose(); throw; }
        if (exitHandlerAttached) return;
        exitHandlerAttached = true;
        desktop.Exit += async (_, _) =>
        {
            if (serviceProvider != null) await serviceProvider.DisposeAsync();
            serviceProvider = null;
        };
    }
    private static ILicenseService CreateLicenseService(string dataDirectory)
    {
        try
        {
            var store = new FileLicenseStore(Path.Combine(dataDirectory, "Licensing"));
            using var stream = typeof(App).Assembly.GetManifestResourceStream("LedgerNest.Licensing.PublicKey");
            using var reader = stream is null ? null : new StreamReader(stream);
            return new LicenseService(new LicenseVerifier(reader?.ReadToEnd() ?? ""), store, store.GetDeviceId());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AppErrorLog.Write(ex, "Initializing license storage");
            return new UnavailableLicenseService("License storage or device identity is unavailable. Contact your software provider.");
        }
    }
}
