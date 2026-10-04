using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Performs the close overlay action for this screen or workflow.
    internal void CloseOverlay()
    {
        if (!Model.CanAccessWorkspace) { ShowAccessScreen(); return; }
        Model.CurrentOverlay = null;
    }

    // Performs the show access screen action for this screen or workflow.
    private void ShowAccessScreen()
    {
        page.Content = null;
        sidebar.Content = null;
        page.IsEnabled = false;
        sidebar.IsEnabled = false;
        if (Model.CurrentUsername == null) ShowLogin(); else ShowChangePassword();
    }

    // Performs the refresh workspace access action for this screen or workflow.
    private void RefreshWorkspaceAccess()
    {
        page.IsEnabled = Model.CanAccessWorkspace;
        sidebar.IsEnabled = Model.CanAccessWorkspace;
        if (!Model.CanAccessWorkspace) { ShowAccessScreen(); return; }
        BuildSidebar();
        ShowPage();
    }
    // Performs the show overlay action for this screen or workflow.
    internal void ShowOverlay(string title, object content, object? footer = null, bool side = false, double width = 560, object? headerAccessory = null, object? leadingIcon = null, bool prominentHeader = false)
    {
        var availableWidth = Bounds.Width - (side ? sidebar.Bounds.Width : 0);
        Model.CurrentOverlay = new OverlayDialogViewModel(title, content, footer, headerAccessory,
            leadingIcon, CloseOverlay, side, prominentHeader, content is ProductEditorFormView,
            width, availableWidth, Bounds.Height);
    }
    // Performs the confirm action for this screen or workflow.
    internal void Confirm(string title, string message, Action action)
    {
        ShowOverlay(title, new DialogMessage(message), new DialogActions([
            new DialogAction("Cancel", new RelayCommand(CloseOverlay)),
            new DialogAction("Confirm", new RelayCommand(() => { action(); CloseOverlay(); }), true)
        ]), width: 460);
    }
}
