using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PageHeaderView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PageHeaderView, string>(nameof(Title), "");
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly StyledProperty<object?> HeaderActionsProperty = AvaloniaProperty.Register<PageHeaderView, object?>(nameof(HeaderActions));
    public object? HeaderActions { get => GetValue(HeaderActionsProperty); set => SetValue(HeaderActionsProperty, value); }
    public static readonly StyledProperty<IEnumerable<Control>?> ActionsProperty = AvaloniaProperty.Register<PageHeaderView, IEnumerable<Control>?>(nameof(Actions));
    public IEnumerable<Control>? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    public PageHeaderView() => InitializeComponent();

    public PageHeaderView(string title, IEnumerable<Control> actions) : this()
    {
        Title = title;
        Actions = actions;
    }
}
