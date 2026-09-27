using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts a settings rail and content area with a compact single-column layout for narrow windows.</summary>
public sealed partial class SettingsWorkspaceView : UserControl
{
    private readonly bool railAfterContentOnNarrow;

    public SettingsWorkspaceView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
    }

    public SettingsWorkspaceView(Control rail, Control content, bool railAfterContentOnNarrow = false)
        : this()
    {
        RailHost.Content = rail;
        ContentHost.Content = content;
        this.railAfterContentOnNarrow = railAfterContentOnNarrow;
        AttachedToVisualTree += (_, _) => ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var narrow = Bounds.Width > 0 && Bounds.Width < 760;
        WorkspaceGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "240,*");
        WorkspaceGrid.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");
        Grid.SetColumn(RailHost, 0);
        Grid.SetColumn(ContentHost, narrow ? 0 : 1);
        Grid.SetRow(RailHost, narrow && railAfterContentOnNarrow ? 1 : 0);
        Grid.SetRow(ContentHost, narrow && railAfterContentOnNarrow ? 0 : narrow ? 1 : 0);
    }
}
