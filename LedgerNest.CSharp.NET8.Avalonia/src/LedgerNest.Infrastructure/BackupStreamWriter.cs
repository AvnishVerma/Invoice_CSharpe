namespace LedgerNest.Infrastructure;

public static class BackupStreamWriter
{
    public static async Task WriteAsync(Stream destination, ReadOnlyMemory<byte> contents)
    {
        if (!destination.CanWrite) throw new IOException("Backup destination is not writable.");
        if (destination.CanSeek)
        {
            destination.Position = 0;
            destination.SetLength(0);
        }
        await destination.WriteAsync(contents);
        await destination.FlushAsync();
    }
}
