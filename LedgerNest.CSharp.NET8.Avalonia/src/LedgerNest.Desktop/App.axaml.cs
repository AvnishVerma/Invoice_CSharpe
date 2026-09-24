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

    // Performs the on framework initialization completed action for this screen or workflow.
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LedgerNest",
                "ledgernest.db");

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            serviceProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddInfrastructure(databasePath)
                .AddSingleton<IPdfGenerator, TemporaryPdfGenerator>()
                .AddSingleton<IPrintServiceFactory, PrintServiceFactory>()
                .AddSingleton<IToastService, AvaloniaToastService>()
                .AddSingleton<ILicenseService>(_ => CreateLicenseService(Path.GetDirectoryName(databasePath)!))
                .BuildServiceProvider();

            var printService = serviceProvider.GetRequiredService<IPrintServiceFactory>().Create();
            desktop.MainWindow = new MainWindow(printService, serviceProvider.GetRequiredService<IPdfGenerator>(), serviceProvider.GetRequiredService<IToastService>())
            {
                DataContext = new MainWindowViewModel(serviceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<LedgerNestDbContext>>(), databasePath, serviceProvider.GetRequiredService<ILicenseService>())
            };
            desktop.Exit += async (_, _) =>
            {
                if (serviceProvider != null) await serviceProvider.DisposeAsync();
                serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
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
