using System.Text.Json;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

internal static class DatabaseProfileStore
{
    public static DatabaseProfile? Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<DatabaseProfile>(File.ReadAllText(path)) : null; }
        catch (JsonException) { return null; }
    }

    public static void Save(string path, DatabaseProfile profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(profile));
    }
}
