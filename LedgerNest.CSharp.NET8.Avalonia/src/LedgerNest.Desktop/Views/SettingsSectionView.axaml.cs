using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>AXAML section frame; callers supply existing fields and commands.</summary>
public sealed partial class SettingsSectionView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<SettingsSectionView, string>(nameof(Title), "");
    public static readonly StyledProperty<string> DescriptionProperty = AvaloniaProperty.Register<SettingsSectionView, string>(nameof(Description), "");
    public static readonly StyledProperty<object?> BodyProperty = AvaloniaProperty.Register<SettingsSectionView, object?>(nameof(Body));
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public SettingsSectionView() => InitializeComponent();
}
