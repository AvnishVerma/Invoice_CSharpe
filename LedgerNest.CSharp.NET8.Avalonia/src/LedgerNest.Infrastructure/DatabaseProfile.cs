namespace LedgerNest.Infrastructure;

public enum DatabaseProvider { Sqlite, MySql, SqlServer }

public sealed record DatabaseProfile(DatabaseProvider Provider, string ConnectionString)
{
    public static DatabaseProfile LocalSqlite(string databasePath) => new(DatabaseProvider.Sqlite, $"Data Source={databasePath}");
}
