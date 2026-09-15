namespace LedgerNest.Infrastructure;

public static class BackupStreamWriter
{
    // Performs the write async action for this screen or workflow.
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
