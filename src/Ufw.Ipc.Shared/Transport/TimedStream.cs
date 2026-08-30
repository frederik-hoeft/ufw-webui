namespace Ufw.Ipc.Shared.Transport;

/// <summary>
/// Applies independent read/write timeouts to an inner stream without requiring
/// the inner stream to implement <see cref="Stream.CanTimeout"/>.
/// Does not own the inner stream.
/// </summary>
public class TimedStream : Stream
{
    private readonly Stream _innerStream;
    private TimeSpan _readTimeout;
    private TimeSpan _writeTimeout;

    public TimedStream(Stream innerStream, TimeSpan readTimeout, TimeSpan writeTimeout)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ValidateTimeout(readTimeout, nameof(readTimeout));
        ValidateTimeout(writeTimeout, nameof(writeTimeout));
        _innerStream = innerStream;
        _readTimeout = readTimeout;
        _writeTimeout = writeTimeout;
    }

    public override bool CanTimeout => true;

    public override int ReadTimeout
    {
        get => (int)_readTimeout.TotalMilliseconds;
        set
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(value);
            ValidateTimeout(timeout, nameof(value));
            _readTimeout = timeout;
        }
    }

    public override int WriteTimeout
    {
        get => (int)_writeTimeout.TotalMilliseconds;
        set
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(value);
            ValidateTimeout(timeout, nameof(value));
            _writeTimeout = timeout;
        }
    }

    public override bool CanRead => _innerStream.CanRead;

    public override bool CanSeek => _innerStream.CanSeek;

    public override bool CanWrite => _innerStream.CanWrite;

    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush() => _innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

    public override void SetLength(long value) => _innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        using CancellationTokenSource? timeout = LinkTimeout(_readTimeout, cancellationToken);
        try
        {
            await _innerStream.CopyToAsync(destination, bufferSize, timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The read operation timed out.");
        }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource? timeout = LinkTimeout(_writeTimeout, cancellationToken);
        try
        {
            await _innerStream.FlushAsync(timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The write operation timed out.");
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        using CancellationTokenSource? timeout = LinkTimeout(_readTimeout, cancellationToken);
        try
        {
            return await _innerStream.ReadAsync(buffer.AsMemory(offset, count), timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The read operation timed out.");
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource? timeout = LinkTimeout(_readTimeout, cancellationToken);
        try
        {
            return await _innerStream.ReadAsync(buffer, timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The read operation timed out.");
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        using CancellationTokenSource? timeout = LinkTimeout(_writeTimeout, cancellationToken);
        try
        {
            await _innerStream.WriteAsync(buffer.AsMemory(offset, count), timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The write operation timed out.");
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource? timeout = LinkTimeout(_writeTimeout, cancellationToken);
        try
        {
            await _innerStream.WriteAsync(buffer, timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The write operation timed out.");
        }
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        _innerStream.BeginRead(buffer, offset, count, callback, state);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        _innerStream.BeginWrite(buffer, offset, count, callback, state);

    public override void CopyTo(Stream destination, int bufferSize) => _innerStream.CopyTo(destination, bufferSize);

    public override int EndRead(IAsyncResult asyncResult) => _innerStream.EndRead(asyncResult);

    public override void EndWrite(IAsyncResult asyncResult) => _innerStream.EndWrite(asyncResult);

    public override int Read(Span<byte> buffer) => _innerStream.Read(buffer);

    public override int ReadByte() => _innerStream.ReadByte();

    public override void Write(ReadOnlySpan<byte> buffer) => _innerStream.Write(buffer);

    public override void WriteByte(byte value) => _innerStream.WriteByte(value);

    private static void ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout != Timeout.InfiniteTimeSpan && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Timeout must be positive or Timeout.InfiniteTimeSpan.");
        }
    }

    private static CancellationTokenSource? LinkTimeout(TimeSpan ioTimeout, CancellationToken cancellationToken)
    {
        if (ioTimeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }

        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(ioTimeout);
        return linked;
    }
}
