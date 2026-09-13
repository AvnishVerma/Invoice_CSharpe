using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LedgerNest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerNest.Desktop;

public partial class App : Avalonia.Application
{
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

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LedgerNest",
                "ledgernest.db");

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddInfrastructure(databasePath)
                .BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<LedgerNestDbContext>>(), databasePath)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
