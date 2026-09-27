using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts application identity, update-channel and notification content in the settings UI.</summary>
public sealed partial class SoftwareInformationView : UserControl
{
    public SoftwareInformationView()
    {
        InitializeComponent();
    }

    public SoftwareInformationView(Control logo, Control details, Control updates, Control notifications, Control actions)
        : this()
    {
        LogoHost.Content = logo;
        DetailsHost.Content = details;
        UpdatesHost.Content = updates;
        NotificationsHost.Content = notifications;
        ActionsHost.Content = actions;
    }
}
