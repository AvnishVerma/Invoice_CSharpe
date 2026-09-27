using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ManagementActionBarView : UserControl
{
    public ManagementActionBarView()
    {
        InitializeComponent();
    }

    public ManagementActionBarView(string title, params Control[] actions) : this()
    {
        TitleText.Text = title;
        foreach (var action in actions) ActionsHost.Children.Add(action);
    }
}
