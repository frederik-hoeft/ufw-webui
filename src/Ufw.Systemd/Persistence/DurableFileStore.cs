namespace Ufw.Systemd.Persistence;

internal sealed class DurableFileStore : IDurableFileStore
{
    private const int BUFFER_SIZE = 4096;

    public async Task AppendAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            EnsureParentDirectory(path);
            await using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(contents, cancellationToken);
#pragma warning disable CA1849 // Flush(bool) is intentionally synchronous to guarantee durable persistence before the operation returns.
            stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateIOException(path, exception);
        }
    }

    public async Task ReplaceAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string temporaryPath = GetTemporaryPath(path);
        try
        {
            EnsureParentDirectory(path);
            await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(contents, cancellationToken);
#pragma warning disable CA1849 // Flush(bool) is intentionally synchronous to guarantee durable persistence before the atomic replacement.
                stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (UnauthorizedAccessException exception)
        {
            TryDelete(temporaryPath);
            throw CreateIOException(path, exception);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    public bool TryCreateNew(string path, ReadOnlyMemory<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FileStream? stream = null;
        bool created = false;
        bool completed = false;
        try
        {
            EnsureParentDirectory(path);
            try
            {
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, BUFFER_SIZE, FileOptions.WriteThrough);
                created = true;
            }
            catch (IOException) when (File.Exists(path))
            {
                return false;
            }

            stream.Write(contents.Span);
            stream.Flush(flushToDisk: true);
            completed = true;
            return true;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateIOException(path, exception);
        }
        finally
        {
            stream?.Dispose();
            if (created && !completed)
            {
                TryDelete(path);
            }
        }
    }

    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            File.Delete(path);
            File.Delete(GetTemporaryPath(path));
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateIOException(path, exception);
        }
    }

    private static string GetTemporaryPath(string path) => path + ".tmp";

    private static void EnsureParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Preserve the primary write/replace failure. A later operation will overwrite or clean the staging file.
        }
    }

    private static IOException CreateIOException(string path, UnauthorizedAccessException exception) => new($"Durable file state at '{path}' could not be persisted.", exception);
}
