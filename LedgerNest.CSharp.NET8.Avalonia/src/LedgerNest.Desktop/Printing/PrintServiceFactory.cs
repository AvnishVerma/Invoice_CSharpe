namespace LedgerNest.Desktop.Printing;

// Keeps operating-system selection in one composition boundary.
public sealed class PrintServiceFactory : IPrintServiceFactory
{
    public IPrintService Create()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return new WindowsPrintService();
        if (OperatingSystem.IsMacOS()) return new MacPrintService();
        if (OperatingSystem.IsLinux()) return new LinuxPrintService();
        throw new PlatformNotSupportedException("Printing is supported on Windows, macOS, and Linux.");
    }
}
