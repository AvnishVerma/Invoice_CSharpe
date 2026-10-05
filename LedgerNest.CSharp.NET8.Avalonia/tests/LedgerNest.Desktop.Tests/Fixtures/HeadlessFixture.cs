using Avalonia;
using Avalonia.Headless;
using LedgerNest.Desktop;

namespace LedgerNest.Desktop.Tests.Fixtures;

public sealed class HeadlessFixture : IDisposable
{
    public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(HeadlessEntryPoint));
    public Task Run(Action action) => Session.Dispatch(action, CancellationToken.None);
    public void Dispose() => Session.Dispose();
}
public static class HeadlessEntryPoint
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
[CollectionDefinition("AXAML UI", DisableParallelization = true)]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessFixture>;
