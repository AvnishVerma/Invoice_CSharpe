using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Application;

/// <summary>One expiry clock and one renewal at a time. The host supplies authentication and a UI-thread tick.</summary>
public sealed class SessionExpiryMonitor : ObservableObject, IDisposable
{
    public static readonly TimeSpan WarningPeriod = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan LocalSessionLifetime = TimeSpan.FromMinutes(30);
    private readonly TimeProvider clock;
    private readonly Func<CancellationToken, Task<DateTimeOffset?>> renew;
    private readonly Action<string> logout;
    private CancellationTokenSource? cancellation;
    private long generation;
    private bool refreshing;
    private int renewalGate;
    private bool visible;
    private DateTimeOffset? successUntil;
    private string? terminationStatus;
    public bool IsExpired => terminationStatus?.StartsWith("Your session has expired", StringComparison.Ordinal) == true;
    public string Title => IsExpired ? "Your session has expired" : "Your Session Is About to Expire";
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsActive => ExpiresAt.HasValue;
    public TimeSpan Remaining => ExpiresAt is { } expiry ? TimeSpan.FromTicks(Math.Max(0, (expiry - clock.GetUtcNow()).Ticks)) : TimeSpan.Zero;
    public bool IsValid => IsActive && Remaining > TimeSpan.Zero;
    public string Countdown => $"{(int)Math.Ceiling(Remaining.TotalSeconds) / 60:00}:{(int)Math.Ceiling(Remaining.TotalSeconds) % 60:00}";
    public double Progress => Math.Clamp(Remaining.TotalSeconds / WarningPeriod.TotalSeconds * 100, 0, 100);
    public bool IsVisible => visible;
    public bool IsRefreshing => refreshing;
    public bool IsSuccess => successUntil.HasValue;
    public bool CanInteract => visible && IsValid && !refreshing && !IsSuccess;
    public bool IsAttention => !IsSuccess && Remaining.TotalSeconds is > 60 and <= 120;
    public bool IsUrgent => !IsSuccess && Remaining.TotalSeconds <= 60;
    public string Status => terminationStatus ?? (IsSuccess ? "Session extended. You're ready to continue." : refreshing ? "Extending your session..." : "Would you like to continue your session?");
    public AsyncRelayCommand ContinueCommand { get; }
    public RelayCommand ExitCommand { get; }

    public SessionExpiryMonitor(TimeProvider clock, Func<CancellationToken, Task<DateTimeOffset?>> renew, Action<string> logout)
    {
        this.clock = clock; this.renew = renew; this.logout = logout;
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, () => CanInteract);
        ExitCommand = new RelayCommand(() => { if (CanInteract) End("Signed out."); }, () => CanInteract);
    }

    public void Start(DateTimeOffset expiry)
    {
        terminationStatus = null;
        Stop();
        cancellation = new CancellationTokenSource();
        ExpiresAt = expiry;
        Tick();
    }

    public void Stop()
    {
        generation++;
        Interlocked.Exchange(ref renewalGate, 0);
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        ExpiresAt = null; visible = false; refreshing = false; successUntil = null;
        Notify();
    }

    public void Tick()
    {
        if (!IsActive) return;
        if (!IsValid) { End("Your session has expired. Please sign in again."); return; }
        if (successUntil is { } success)
        {
            if (clock.GetUtcNow() >= success) { successUntil = null; visible = false; }
        }
        else if (Remaining <= WarningPeriod) visible = true;
        Notify();
    }

    public async Task ContinueAsync()
    {
        if (Interlocked.CompareExchange(ref renewalGate, 1, 0) != 0) return;
        if (!CanInteract) { Interlocked.Exchange(ref renewalGate, 0); return; }
        var version = generation;
        var token = cancellation!.Token;
        refreshing = true; Notify();
        try
        {
            var renewedExpiry = await renew(token);
            if (version != generation || token.IsCancellationRequested) return;
            // The old deadline still applies while renewal is in flight.
            if (!IsValid) { End("Your session has expired. Please sign in again."); return; }
            if (renewedExpiry is null || renewedExpiry <= clock.GetUtcNow() || renewedExpiry <= ExpiresAt)
            { End("Unable to extend your session. Please sign in again."); return; }
            ExpiresAt = renewedExpiry;
            successUntil = clock.GetUtcNow().AddMilliseconds(350);
        }
        catch (Exception)
        {
            if (version == generation) End("Unable to extend your session. Please sign in again.");
        }
        finally
        {
            if (version == generation) { refreshing = false; Interlocked.Exchange(ref renewalGate, 0); Notify(); }
        }
    }

    private void End(string message) { Stop(); terminationStatus = message; Notify(); logout(message); }
    private void Notify()
    {
        foreach (var property in new[] { nameof(Title), nameof(IsExpired), nameof(ExpiresAt), nameof(IsActive), nameof(IsValid), nameof(Remaining), nameof(Countdown), nameof(Progress), nameof(IsVisible), nameof(IsRefreshing), nameof(IsSuccess), nameof(CanInteract), nameof(IsAttention), nameof(IsUrgent), nameof(Status) })
            OnPropertyChanged(property);
        ContinueCommand.NotifyCanExecuteChanged(); ExitCommand.NotifyCanExecuteChanged();
    }
    public void Dispose() => Stop();
}
