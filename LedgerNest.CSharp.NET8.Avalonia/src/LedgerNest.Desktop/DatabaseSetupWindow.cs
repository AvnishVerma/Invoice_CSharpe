using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LedgerNest.Infrastructure;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

internal sealed class DatabaseSetupWindow : Window
{
    public DatabaseSetupWindow(string defaultDatabasePath, Action<DatabaseProfile> configured)
    {
        Title = "Choose Database";
        Width = 620;
        Height = 430;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var provider = new ComboBox { ItemsSource = new[] { "SQLite", "MySQL", "SQL Server" }, SelectedIndex = 0, MinHeight = 32 };
        var connection = new TextBox { Text = $"Data Source={defaultDatabasePath}", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 74 };
        var help = Ui.Text("SQLite stores data on this computer. MySQL and SQL Server let multiple LedgerNest clients share one database.", 13, color: Ui.Muted);
        provider.SelectionChanged += (_, _) =>
        {
            connection.Text = provider.SelectedIndex switch
            {
                1 => "Server=localhost;Database=ledgernest;User ID=ledgernest;Password=",
                2 => "Server=localhost;Database=LedgerNest;Trusted_Connection=True;TrustServerCertificate=True",
                _ => $"Data Source={defaultDatabasePath}"
            };
            help.Text = provider.SelectedIndex == 0
                ? "SQLite is local to this computer. Choose a server database for multi-client access."
                : "Use a database server reachable by every client. LedgerNest creates its schema when it first connects.";
        };
        var status = Ui.Text("", 12, color: Avalonia.Media.Brushes.IndianRed);
        var save = Ui.Button("Continue", () =>
        {
            if (string.IsNullOrWhiteSpace(connection.Text)) { status.Text = "Enter a connection string."; return; }
            var selected = provider.SelectedIndex switch { 1 => DatabaseProvider.MySql, 2 => DatabaseProvider.SqlServer, _ => DatabaseProvider.Sqlite };
            configured(new DatabaseProfile(selected, connection.Text.Trim()));
            Close();
        }, true);
        Content = new Border { Padding = new Thickness(28), Child = Ui.Stack(14,
            Ui.Text("Database setup", 24, true),
            Ui.Text("Choose where LedgerNest stores shared business data. This is shown only before the first connection.", 13, color: Ui.Muted),
            Ui.Text("Database provider", 12, true, Ui.Muted), provider,
            Ui.Text("Connection string", 12, true, Ui.Muted), connection, help, status,
            new Border { Height = 8 }, save) };
    }
}
