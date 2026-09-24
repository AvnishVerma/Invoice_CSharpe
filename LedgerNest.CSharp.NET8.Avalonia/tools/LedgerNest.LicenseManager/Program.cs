using Avalonia;

namespace LedgerNest.LicenseManager;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<PublisherApp>().UsePlatformDetect().LogToTrace();
}
