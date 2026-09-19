using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LedgerNest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
                .AddSingleton<IHtmlPrintService, PlaywrightHtmlPrintService>()
                .BuildServiceProvider();

            var printService = serviceProvider.GetRequiredService<IHtmlPrintService>();
            desktop.MainWindow = new MainWindow(printService)
            {
                DataContext = new MainWindowViewModel(serviceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<LedgerNestDbContext>>(), databasePath)
            };
            desktop.MainWindow.Opened += async (_, _) =>
            {
                try { await printService.WarmUpAsync(); }
                catch (Exception ex) { AppErrorLog.Write(ex, "Warming up the HTML print service"); }
            };
            desktop.Exit += async (_, _) =>
            {
                if (serviceProvider != null) await serviceProvider.DisposeAsync();
                serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
