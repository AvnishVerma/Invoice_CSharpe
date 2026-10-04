using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

public sealed partial class ActionButtonView : Button
{
    public static readonly StyledProperty<string> ActionLabelProperty = AvaloniaProperty.Register<ActionButtonView, string>(nameof(ActionLabel), "");
    public static readonly StyledProperty<string> CaptionProperty = AvaloniaProperty.Register<ActionButtonView, string>(nameof(Caption), "");
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<ActionButtonView, string>(nameof(Icon), "");
    public static readonly StyledProperty<bool> PrimaryProperty = AvaloniaProperty.Register<ActionButtonView, bool>(nameof(Primary));
    public static readonly StyledProperty<bool> AvailableProperty = AvaloniaProperty.Register<ActionButtonView, bool>(nameof(Available), true);
    public static readonly StyledProperty<string> TooltipProperty = AvaloniaProperty.Register<ActionButtonView, string>(nameof(Tooltip), "");
    public string ActionLabel { get => GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool Primary { get => GetValue(PrimaryProperty); set => SetValue(PrimaryProperty, value); }
    public bool Available { get => GetValue(AvailableProperty); set => SetValue(AvailableProperty, value); }
    public string Tooltip { get => GetValue(TooltipProperty); set => SetValue(TooltipProperty, value); }
    public bool HasIcon => Icon.Length > 0;
    public bool HasCaption => Caption.Length > 0;
    protected override Type StyleKeyOverride => typeof(Button);
    public ActionButtonView() => InitializeComponent();
    internal ActionButtonView(string label, Action? action, bool primary) : this()
    {
        var symbols = new Dictionary<char, string> { ['＋'] = "add", ['↑'] = "upload", ['↓'] = "download", ['↻'] = "refresh", ['×'] = "close", ['‹'] = "chevron_left", ['›'] = "chevron_right", ['⋯'] = "more_horiz", ['⇥'] = "logout" };
        ActionLabel = label;
        Icon = label.Length > 0 ? symbols.GetValueOrDefault(label[0], "") : "";
        Caption = Icon.Length > 0 ? label[1..].Trim() : label;
        Primary = primary;
        Available = action != null;
        var iconTooltips = new Dictionary<string, string> { ["add"] = "Add", ["upload"] = "Upload", ["download"] = "Download",
            ["refresh"] = "Refresh", ["close"] = "Close", ["chevron_left"] = "Previous", ["chevron_right"] = "Next",
            ["more_horiz"] = "More actions", ["logout"] = "Sign out" };
        Tooltip = Available ? (Caption.Length > 0 ? Caption : iconTooltips.GetValueOrDefault(Icon, label))
            : "This service has not yet been migrated.";
        Command = action == null ? null : new RelayCommand(action);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) RaisePropertyChanged(HasIconProperty, ((string?)change.OldValue)?.Length > 0, HasIcon);
        if (change.Property == CaptionProperty) RaisePropertyChanged(HasCaptionProperty, ((string?)change.OldValue)?.Length > 0, HasCaption);
    }
    public static readonly DirectProperty<ActionButtonView, bool> HasIconProperty = AvaloniaProperty.RegisterDirect<ActionButtonView, bool>(nameof(HasIcon), view => view.HasIcon);
    public static readonly DirectProperty<ActionButtonView, bool> HasCaptionProperty = AvaloniaProperty.RegisterDirect<ActionButtonView, bool>(nameof(HasCaption), view => view.HasCaption);
}
