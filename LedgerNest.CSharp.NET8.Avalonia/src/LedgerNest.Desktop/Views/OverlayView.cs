using Avalonia;
using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    internal void CloseOverlay()
    {
        if (!Model.CanAccessWorkspace) { ShowAccessScreen(); return; }
        overlay.Children.Clear(); overlay.IsVisible = false;
    }

    private void ShowAccessScreen()
    {
        page.Content = null;
        sidebar.Content = null;
        page.IsEnabled = false;
        sidebar.IsEnabled = false;
        if (Model.CurrentUsername == null) ShowLogin(); else ShowChangePassword();
    }

    private void RefreshWorkspaceAccess()
    {
        page.IsEnabled = Model.CanAccessWorkspace;
        sidebar.IsEnabled = Model.CanAccessWorkspace;
        if (!Model.CanAccessWorkspace) { ShowAccessScreen(); return; }
        BuildSidebar();
        ShowPage();
    }
    internal void ShowOverlay(string title, Control content, Control? footer = null, bool side = false, double width = 560, Control? headerAccessory = null)
    {
        overlay.Children.Clear(); overlay.IsVisible = true;
        overlay.Margin = new Thickness(side ? (Model.SidebarExpanded ? 210 : 64) : 0, 0, 0, 0);
        var availableWidth = Bounds.Width - overlay.Margin.Left;
        overlay.Children.Add(new OverlayDialogView(title, content, footer ?? Ui.Button("Close", CloseOverlay), headerAccessory, CloseOverlay, side, width, availableWidth, Bounds.Height));
    }
    internal void Confirm(string title, string message, Action action)
    { ShowOverlay(title, Ui.Text(message), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Confirm", () => { action(); CloseOverlay(); }, true)), width: 460); }
}
