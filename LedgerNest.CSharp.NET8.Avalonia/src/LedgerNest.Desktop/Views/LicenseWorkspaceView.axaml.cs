using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts a license-state notice above the current workspace content.</summary>
public sealed partial class LicenseWorkspaceView : UserControl
{
    public LicenseWorkspaceView()
    {
        InitializeComponent();
    }

    public LicenseWorkspaceView(Control notice, Control body)
        : this()
    {
        NoticeHost.Content = notice;
        BodyHost.Content = body;
    }
}
