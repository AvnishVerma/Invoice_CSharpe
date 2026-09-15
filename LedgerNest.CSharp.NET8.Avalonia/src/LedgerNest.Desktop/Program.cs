using Avalonia;

namespace LedgerNest.Desktop;

internal static class Program
{
    [STAThread]
    // Performs the main action for this screen or workflow.
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Performs the build avalonia app action for this screen or workflow.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
