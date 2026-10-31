using LedgerNest.Application;

namespace LedgerNest.Desktop.Tests.UI;

public sealed class SessionExpiryMonitorTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }

    [Fact]
    public void WarningUsesDeadlineAndRecalculatesAfterSleep()
    {
        var clock = new Clock(); var exits = 0;
        using var monitor = new SessionExpiryMonitor(clock, _ => Task.FromResult<DateTimeOffset?>(null), _ => exits++);
        monitor.Start(clock.Now.AddMinutes(30));
        clock.Advance(1499); monitor.Tick(); Assert.False(monitor.IsVisible);
        clock.Advance(1); monitor.Tick(); Assert.True(monitor.IsVisible); Assert.Equal("05:00", monitor.Countdown);
        clock.Advance(1); monitor.Tick(); Assert.Equal("04:59", monitor.Countdown);
        clock.Advance(179); monitor.Tick(); Assert.True(monitor.IsAttention);
        clock.Advance(60); monitor.Tick(); Assert.True(monitor.IsUrgent);
        clock.Advance(70); monitor.Tick(); Assert.Equal(1, exits); Assert.False(monitor.IsActive);
        monitor.Tick(); Assert.Equal(1, exits);
    }

    [Fact]
    public async Task RenewalIsSingleFlightAndSchedulesNextWarning()
    {
        var clock = new Clock(); var calls = 0;
        var response = new TaskCompletionSource<DateTimeOffset?>();
        using var monitor = new SessionExpiryMonitor(clock, _ => { calls++; return response.Task; }, _ => Assert.Fail("Unexpected logout"));
        monitor.Start(clock.Now.AddMinutes(5));
        var first = monitor.ContinueAsync(); var second = monitor.ContinueAsync();
        Assert.True(monitor.IsRefreshing); Assert.False(monitor.ExitCommand.CanExecute(null)); Assert.Equal(1, calls);
        response.SetResult(clock.Now.AddMinutes(30)); await Task.WhenAll(first, second);
        Assert.True(monitor.IsSuccess); Assert.True(monitor.IsValid);
        clock.Advance(1); monitor.Tick(); Assert.False(monitor.IsVisible);
        clock.Advance(1499); monitor.Tick(); Assert.True(monitor.IsVisible); Assert.Equal("05:00", monitor.Countdown);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidOrFailedRenewalLogsOut(bool throws)
    {
        var clock = new Clock(); var exits = 0;
        using var monitor = new SessionExpiryMonitor(clock, _ => throws ? Task.FromException<DateTimeOffset?>(new InvalidOperationException()) : Task.FromResult<DateTimeOffset?>(null), _ => exits++);
        monitor.Start(clock.Now.AddSeconds(15)); await monitor.ContinueAsync();
        Assert.Equal(1, exits); Assert.False(monitor.IsActive); Assert.False(monitor.IsRefreshing);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LateRenewalCannotRestoreExpiredOrLoggedOutSession(bool logout)
    {
        var clock = new Clock(); var exits = 0;
        var response = new TaskCompletionSource<DateTimeOffset?>();
        using var monitor = new SessionExpiryMonitor(clock, _ => response.Task, _ => exits++);
        monitor.Start(clock.Now.AddSeconds(10)); var pending = monitor.ContinueAsync();
        if (logout) monitor.Stop(); else { clock.Advance(11); monitor.Tick(); }
        response.SetResult(clock.Now.AddHours(1)); await pending;
        Assert.False(monitor.IsActive); Assert.False(monitor.IsVisible); Assert.Equal(logout ? 0 : 1, exits);
    }

    [Fact]
    public void ExitAndAlreadyExpiredDeadlineInvalidateImmediately()
    {
        var clock = new Clock(); var exits = 0;
        using var monitor = new SessionExpiryMonitor(clock, _ => Task.FromResult<DateTimeOffset?>(null), _ => exits++);
        monitor.Start(clock.Now.AddSeconds(45)); Assert.True(monitor.IsVisible);
        monitor.ExitCommand.Execute(null); Assert.False(monitor.IsActive); Assert.Equal(1, exits);
        monitor.Start(clock.Now.AddSeconds(-1)); Assert.False(monitor.IsActive); Assert.Equal(2, exits);
    }

    [Fact]
    public async Task DeadlineIsEnforcedEvenWithoutTimerDuringRenewal()
    {
        var clock = new Clock(); var exits = 0; var response = new TaskCompletionSource<DateTimeOffset?>();
        using var monitor = new SessionExpiryMonitor(clock, _ => response.Task, _ => exits++);
        monitor.Start(clock.Now.AddSeconds(1)); var pending = monitor.ContinueAsync();
        clock.Advance(2); response.SetResult(clock.Now.AddMinutes(30)); await pending;
        Assert.False(monitor.IsActive); Assert.Equal(1, exits);
    }
}
