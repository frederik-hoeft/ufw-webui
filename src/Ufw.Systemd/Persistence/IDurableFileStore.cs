namespace Ufw.Systemd.Persistence;

internal interface IDurableFileStore
{
    Task AppendAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken);

    Task ReplaceAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken);

    bool TryCreateNew(string path, ReadOnlyMemory<byte> contents);

    void Delete(string path);
}
