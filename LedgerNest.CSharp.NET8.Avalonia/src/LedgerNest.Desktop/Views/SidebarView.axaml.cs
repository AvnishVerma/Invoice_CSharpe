using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    public SidebarView(MainWindowViewModel model, Action<string> navigate, Action toggleSidebar, Action logout)
    {
        InitializeComponent();
        var expanded = model.SidebarExpanded;
        Shell.Width = expanded ? 210 : 64;
        LogoPanel.Padding = new Thickness(expanded ? 12 : 0, 0);
        LogoHost.Content = expanded
            ? Ui.Columns("*,Auto", Ui.Logo(), Ui.Button("‹", toggleSidebar))
            : Ui.Stack(2, Ui.Logo(true), Ui.Button("›", toggleSidebar));

        var nav = Ui.Stack(0);
        string[] icons = ["dashboard", "receipt", "receipt_long", "request_quote", "point_of_sale", "people", "inventory_2", "bar_chart", "settings"];
        for (var i = 0; i < MainWindowViewModel.Routes.Length; i++)
        {
            var route = MainWindowViewModel.Routes[i];
            var selected = model.Title == route;
            var button = Ui.Button(route, () => navigate(route));
            var marker = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), Background = Ui.Primary, IsVisible = selected && expanded };
            button.Content = expanded
                ? Ui.Columns("18,12,*,Auto", Ui.Icon(icons[i], 18, selected ? Ui.Primary : Ui.Muted), new Border(), Ui.Text(route, 13.5, selected, selected ? Ui.Primary : Ui.Muted), marker)
                : Ui.Icon(icons[i], 20, selected ? Ui.Primary : Ui.Muted);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Classes.Clear();
            button.Classes.Add("nav");
            if (selected) button.Classes.Add("selected");
            ToolTip.SetTip(button, route);
            nav.Children.Add(button);
        }
        NavHost.Content = nav;

        var avatar = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Brush.Parse("#DDE3ED"), Child = Ui.Text(model.CurrentUsername is { Length: > 0 } username ? username[..1].ToUpperInvariant() : "?", 12, true, Ui.Primary) };
        ((TextBlock)avatar.Child!).HorizontalAlignment = HorizontalAlignment.Center;
        var logoutButton = Ui.Button("⇥", logout);
        Avalonia.Automation.AutomationProperties.SetName(logoutButton, model.CurrentUsername == null ? "Sign in" : "Sign out");
        logoutButton.Classes.Add("text");
        logoutButton.Padding = new Thickness(4);
        logoutButton.MinHeight = 30;
        FooterHost.Content = expanded
            ? Ui.Stack(8, Ui.Columns("30,10,*,Auto", avatar, new Border(), Ui.Stack(2, Ui.Text(model.CurrentUsername ?? "Not signed in", 13), Ui.Text(model.CurrentRole, 11, color: Ui.Muted)), logoutButton), new TextBlock { Text = "v4.4.0", FontSize = 12, Foreground = Ui.Outline, HorizontalAlignment = HorizontalAlignment.Center })
            : Ui.Stack(8, avatar, logoutButton);
    }
}
