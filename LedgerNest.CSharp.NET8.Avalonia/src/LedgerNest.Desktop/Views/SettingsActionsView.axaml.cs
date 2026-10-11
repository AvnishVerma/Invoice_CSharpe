using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class SettingsActionsView : UserControl
{
    public static readonly StyledProperty<ICommand?> SaveCommandProperty = AvaloniaProperty.Register<SettingsActionsView, ICommand?>(nameof(SaveCommand));
    public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<SettingsActionsView, ICommand?>(nameof(CancelCommand));
    public static readonly StyledProperty<string> SaveLabelProperty = AvaloniaProperty.Register<SettingsActionsView, string>(nameof(SaveLabel), "Save Settings");
    public ICommand? SaveCommand { get => GetValue(SaveCommandProperty); set => SetValue(SaveCommandProperty, value); }
    public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
    public string SaveLabel { get => GetValue(SaveLabelProperty); set => SetValue(SaveLabelProperty, value); }
    public static readonly DirectProperty<SettingsActionsView, bool> HasCancelProperty = AvaloniaProperty.RegisterDirect<SettingsActionsView, bool>(nameof(HasCancel), view => view.HasCancel);
    public bool HasCancel => CancelCommand != null;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CancelCommandProperty) RaisePropertyChanged(HasCancelProperty, false, HasCancel);
    }
    public SettingsActionsView() => InitializeComponent();
}
