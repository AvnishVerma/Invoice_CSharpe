using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Invoiso.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Invoiso.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Invoiso",
                "invoiso.db");

            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddInfrastructure(databasePath)
                .BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
