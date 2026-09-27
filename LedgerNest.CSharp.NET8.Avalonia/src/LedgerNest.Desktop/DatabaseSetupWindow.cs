using Avalonia.Controls;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

public sealed partial class DatabaseSetupWindow : Window
{
    private readonly string defaultDatabasePath;
    private readonly Action<DatabaseProfile> configured;

    public DatabaseSetupWindow()
        : this(string.Empty, _ => { })
    {
    }

    internal DatabaseSetupWindow(string defaultDatabasePath, Action<DatabaseProfile> configured)
    {
        this.defaultDatabasePath = defaultDatabasePath;
        this.configured = configured;
        InitializeComponent();
        ConnectionBox.Text = SqliteConnectionString;
        HelpText.Text = LocalDatabaseHelp;
    }

    private string SqliteConnectionString => $"Data Source={defaultDatabasePath}";

    private const string LocalDatabaseHelp = "SQLite is local to this computer. Choose a server database for multi-client access.";
    private const string ServerDatabaseHelp = "Use a database server reachable by every client. LedgerNest creates its schema when it first connects.";

    private void ProviderChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Avalonia can raise SelectionChanged while InitializeComponent is still assigning x:Name fields.
        if (ProviderBox is null || ConnectionBox is null || HelpText is null || StatusText is null || ProviderBox.SelectedIndex < 0) return;
        ConnectionBox.Text = ProviderBox.SelectedIndex switch
        {
            1 => "Server=localhost;Database=ledgernest;User ID=ledgernest;Password=",
            2 => "Server=localhost;Database=LedgerNest;Trusted_Connection=True;TrustServerCertificate=True",
            _ => SqliteConnectionString
        };
        HelpText.Text = ProviderBox.SelectedIndex == 0 ? LocalDatabaseHelp : ServerDatabaseHelp;
        StatusText.Text = string.Empty;
    }

    private void ContinueClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ConnectionBox.Text))
        {
            StatusText.Text = "Enter a connection string.";
            return;
        }

        var provider = ProviderBox.SelectedIndex switch
        {
            1 => DatabaseProvider.MySql,
            2 => DatabaseProvider.SqlServer,
            _ => DatabaseProvider.Sqlite
        };
        configured(new DatabaseProfile(provider, ConnectionBox.Text.Trim()));
        Close();
    }
}
