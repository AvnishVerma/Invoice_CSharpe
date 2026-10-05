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
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, profile);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
