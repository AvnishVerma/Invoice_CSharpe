using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Application;

namespace LedgerNest.Desktop.Views;

public partial class SessionExpiryDialogView : UserControl
{
    private SessionExpiryMonitor? monitor;
    private bool shown;
    private IInputElement? previousFocus;
    private ITimer? transitionTimer;
    private long transitionGeneration;
    public TimeProvider TransitionClock { get; set; } = TimeProvider.System;
    public bool ReduceMotion { get; private set; } = ReadReducedMotion();
    public bool? ReducedMotionOverride { get; set; }
    public SessionExpiryDialogView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => BindMonitor();
        AttachedToVisualTree += (_, _) => BindMonitor();
        DetachedFromVisualTree += (_, _) =>
        {
            if (monitor != null) monitor.PropertyChanged -= Updated;
            monitor = null; CancelTransition();
            ClearMotion();
        };
    }
    private void BindMonitor()
    {
        if (monitor != null) monitor.PropertyChanged -= Updated;
        monitor = DataContext as SessionExpiryMonitor;
        if (monitor != null)
        {
            monitor.PropertyChanged += Updated; Updated(monitor, new(nameof(monitor.IsVisible)));
            if (shown) Dispatcher.UIThread.Post(() => { if (shown) ContinueButton.Focus(); });
        }
    }
    private void Updated(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SessionExpiryMonitor.Countdown) && shown && !ReduceMotion)
        {
            CountdownDigits.Transitions = null; CountdownDigits.Opacity = 0.85;
            CountdownDigits.Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(180) },
                new BrushTransition { Property = TextBlock.ForegroundProperty, Duration = TimeSpan.FromMilliseconds(220) }
            };
            CountdownDigits.Opacity = 1;
            return;
        }
        if (monitor == null || args.PropertyName != nameof(monitor.IsVisible) || shown == monitor.IsVisible) return;
        CancelTransition(); shown = monitor.IsVisible;
        if (shown)
        {
            ReduceMotion = ReducedMotionOverride ?? ReadReducedMotion();
            previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            LoadingIndicator.IsIndeterminate = !ReduceMotion; LoadingIndicator.Value = ReduceMotion ? 50 : 0;
            ProgressIndicator.Transitions = ReduceMotion ? null : new Transitions
            {
                new DoubleTransition { Property = ProgressBar.ValueProperty, Duration = TimeSpan.FromMilliseconds(850) },
                new BrushTransition { Property = ProgressBar.ForegroundProperty, Duration = TimeSpan.FromMilliseconds(220) }
            };
            IsVisible = true;
            Dispatcher.UIThread.Post(() => { if (shown) ContinueButton.Focus(); });
        }
        if (ReduceMotion) { CompleteTransition(); return; }
        var scale = (ScaleTransform)Card.RenderTransform!;
        Card.Transitions = null; Backdrop.Transitions = null; scale.Transitions = null;
        Card.Opacity = Backdrop.Opacity = shown ? 0 : 1;
        scale.ScaleX = scale.ScaleY = shown ? 0.97 : 1;
        Card.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) },
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(220) }
        };
        Backdrop.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
        scale.Transitions = new Transitions
        {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromMilliseconds(250) },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromMilliseconds(250) }
        };
        Card.Opacity = Backdrop.Opacity = shown ? 1 : 0;
        scale.ScaleX = scale.ScaleY = shown ? 1 : 0.97;
        // UI completion is independent of rendering-clock completion and never gates authentication.
        var generation = transitionGeneration;
        transitionTimer = TransitionClock.CreateTimer(_ => Dispatcher.UIThread.Post(() =>
        {
            if (generation == transitionGeneration) CompleteTransition();
        }), null, TimeSpan.FromMilliseconds(250), Timeout.InfiniteTimeSpan);
    }
    private void CancelTransition()
    {
        transitionGeneration++;
        transitionTimer?.Dispose(); transitionTimer = null;
    }
    private void CompleteTransition()
    {
        CancelTransition(); ClearMotion();
        Card.Opacity = Backdrop.Opacity = shown ? 1 : 0;
        var scale = (ScaleTransform)Card.RenderTransform!;
        scale.ScaleX = scale.ScaleY = shown ? 1 : 0.97;
        IsVisible = shown;
        if (!shown && previousFocus is Control control && !this.IsVisualAncestorOf(control)) previousFocus.Focus();
    }
    private void ClearMotion()
    {
        Card.Transitions = null; Backdrop.Transitions = null; CountdownDigits.Transitions = null;
        ((ScaleTransform)Card.RenderTransform!).Transitions = null;
    }
    private void OnKeyDown(object? sender, KeyEventArgs args) { if (args.Key == Key.Escape) args.Handled = true; }
    private static bool ReadReducedMotion()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try { return !SystemParametersInfo(0x1042, 0, out var enabled, 0) || !enabled; }
        catch (DllNotFoundException) { return true; }
        catch (EntryPointNotFoundException) { return true; }
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
}
