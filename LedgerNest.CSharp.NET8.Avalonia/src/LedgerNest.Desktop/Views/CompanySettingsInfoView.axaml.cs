using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class CompanySettingsInfoView : UserControl
{
    public CompanySettingsInfoView()
    {
        InitializeComponent();
    }

    public CompanySettingsInfoView(Control appBar, Control logoButton, Control logoPosition, Control previewName, Control save, Control details)
    {
        InitializeComponent();
        AppBarHost.Content = appBar;
        LogoButtonHost.Content = logoButton;
        LogoPositionHost.Content = logoPosition;
        PreviewNameHost.Content = previewName;
        SaveHost.Content = save;
        DetailsHost.Content = details;
    }
}
