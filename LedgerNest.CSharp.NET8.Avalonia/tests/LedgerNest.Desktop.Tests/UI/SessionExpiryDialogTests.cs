using Avalonia.Controls;

using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Application;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class SessionExpiryDialogTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(640, false, 299)]
    [InlineData(1000, true, 100)]
    [InlineData(360, false, 45)]
    public Task DialogRendersThemesStatesAndKeyboardFocus(int width, bool dark, int seconds) => headless.Run(() =>
    {
        var clock = new DialogClock();
        using var monitor = new SessionExpiryMonitor(clock, _ => Task.FromResult<DateTimeOffset?>(clock.Now.AddMinutes(30)), _ => { });
        var view = new SessionExpiryDialogView { DataContext = monitor, ReducedMotionOverride = true };
        var window = new Window { Content = view, Width = width, Height = 740, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); monitor.Start(clock.Now.AddSeconds(seconds)); Settle(window);
            Assert.True(view.IsVisible); Assert.True(view.ReduceMotion);
            var card = view.FindControl<Border>("Card")!;
            Assert.True(card.Bounds.Width <= width - 39);
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(card));
            var action = view.FindControl<Button>("ContinueButton")!;
            Assert.True(action.IsFocused); Assert.True(action.IsEnabled);
            Assert.Equal(monitor.IsAttention, card.Classes.Contains("attention"));
            Assert.Equal(monitor.IsUrgent, card.Classes.Contains("urgent"));
            Assert.Null(view.FindControl<ProgressBar>("ProgressIndicator")!.Transitions);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == monitor.Countdown);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "session-expiry"); Directory.CreateDirectory(output);
            frame!.Save(Path.Combine(output, $"{width}-{dark}-{seconds}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            monitor.Stop(); Settle(window); Assert.False(view.IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ExpiryInShellReturnsToLoginWithoutUnsavedPrompt() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        var window = new MainWindow { DataContext = model };
        try
        {
            window.Show(); Settle(window);
            model.SessionWarning.Start(DateTimeOffset.UtcNow.AddSeconds(-1)); Settle(window);
            Assert.Null(model.CurrentUsername); Assert.False(model.CanAccessWorkspace);
            Assert.IsType<LoginViewModel>(model.CurrentOverlay);
            Assert.Empty(model.VisibleRoutes);
        }
        finally { window.Close(); }
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeEntranceAndExitAnimationsComplete(bool dark)
    {

        var motionClock = new MotionClock();
        SessionExpiryMonitor? monitor = null; SessionExpiryDialogView? view = null; Window? window = null;
        try
        {
            await headless.Run(() =>
            {
                monitor = new SessionExpiryMonitor(TimeProvider.System, _ => Task.FromResult<DateTimeOffset?>(null), _ => { });
                view = new SessionExpiryDialogView { DataContext = monitor, ReducedMotionOverride = false, TransitionClock = motionClock };
                window = new Window { Content = view, Width = 800, Height = 740, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
                window.Show(); monitor.Start(DateTimeOffset.UtcNow.AddMinutes(4)); Settle(window);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
                Assert.False(view.ReduceMotion);
                Assert.NotNull(view.FindControl<ProgressBar>("ProgressIndicator")!.Transitions);
            });
            await PumpUntil(window!, motionClock, () => view!.FindControl<Border>("Card")!.Opacity >= 0.99);
            await headless.Run(() => monitor!.Stop());
            await PumpUntil(window!, motionClock, () => !view!.IsVisible);
        }
        finally { await headless.Run(() => { window?.Close(); monitor?.Dispose(); }); }
    }
    private async Task PumpUntil(Window window, MotionClock clock, Func<bool> complete)
    {
        var done = false;
        for (var frame = 0; frame < 100 && !done; frame++)
        {
                        await headless.Run(() =>
            {
                clock.Advance();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window); done = complete();
            });
            if (!done) await Task.Yield();
        }
        await headless.Run(() =>
        {
            var view = (SessionExpiryDialogView)window.Content!;
            Assert.True(done, $"Transition incomplete: visible={view.IsVisible}, desired={((SessionExpiryMonitor)view.DataContext!).IsVisible}");
        });
    }

    private sealed class MotionClock : TimeProvider
    {
        private TimeSpan now;
        private readonly List<Pending> timers = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Pending(this, callback, state, now + dueTime); timers.Add(timer); return timer;
        }
        public void Advance()
        {
            now += TimeSpan.FromMilliseconds(20);
            foreach (var timer in timers.Where(timer => !timer.Disposed && timer.Due <= now).ToArray()) timer.Fire();
        }
        private sealed class Pending(MotionClock clock, TimerCallback callback, object? state, TimeSpan due) : ITimer
        {
            public TimeSpan Due = due;
            public bool Disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) { if (Disposed) return false; Due = clock.now + dueTime; return true; }
            public void Fire() { Disposed = true; callback(state); }
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class DialogClock : TimeProvider
    {
        public DateTimeOffset Now { get; } = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
}
