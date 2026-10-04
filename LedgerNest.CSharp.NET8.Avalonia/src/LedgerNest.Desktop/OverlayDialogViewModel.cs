using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed record DialogMessage(string Text);
public sealed record DialogAction(string Label, IRelayCommand? Command, bool IsPrimary = false)
{
    public bool IsEnabled => Command != null;
    public string Tooltip => IsEnabled ? Label : "This service has not yet been migrated.";
}
public sealed record DialogActions(IReadOnlyList<DialogAction> Items);

public sealed class OverlayDialogViewModel
{
    public string Title { get; }
    public object Body { get; }
    public object Footer { get; }
    public object? HeaderAccessory { get; }
    public object? LeadingIcon { get; }
    public IRelayCommand CloseCommand { get; }
    public bool IsSide { get; }
    public bool IsProminent { get; }
    public bool IsProductEditor { get; }
    public bool ShowClose => !IsProminent || IsProductEditor;
    public double PanelWidth { get; }
    public double MaximumWidth { get; }
    public double MaximumHeight { get; }

    public OverlayDialogViewModel(string title, object body, object? footer, object? accessory,
        object? leadingIcon, Action close, bool side, bool prominent, bool productEditor,
        double width, double availableWidth, double availableHeight)
    {
        Title = title;
        Body = body;
        CloseCommand = new RelayCommand(close);
        Footer = footer ?? new DialogActions([new DialogAction("Close", CloseCommand)]);
        HeaderAccessory = accessory;
        LeadingIcon = leadingIcon;
        IsSide = side;
        IsProminent = leadingIcon != null || prominent;
        IsProductEditor = productEditor;
        MaximumWidth = Math.Max(280, availableWidth - 32);
        MaximumHeight = Math.Max(320, availableHeight - 32);
        PanelWidth = productEditor ? Math.Min(550, MaximumWidth)
            : side ? (availableWidth < 750 ? availableWidth - 32 : Math.Clamp(availableWidth * .42, 520, 680))
            : width;
    }
}
