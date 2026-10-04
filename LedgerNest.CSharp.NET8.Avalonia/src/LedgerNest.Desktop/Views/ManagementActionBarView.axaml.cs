using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementActionBarView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ManagementActionBarView, string>(nameof(Title), "");
    public static readonly StyledProperty<IEnumerable<Control>?> ActionsProperty = AvaloniaProperty.Register<ManagementActionBarView, IEnumerable<Control>?>(nameof(Actions));
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IEnumerable<Control>? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    public ManagementActionBarView() => InitializeComponent();
    public ManagementActionBarView(string title, params Control[] actions) : this()
    {
        Title = title;
        Actions = actions;
    }
}
