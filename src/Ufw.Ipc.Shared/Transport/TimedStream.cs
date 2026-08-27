namespace Ufw.Ipc.Shared.Transport;

public class TimedStream : Stream
{
    private CancellationTokenSource? _timeoutCts;
    private readonly Stream _innerStream;
    private TimeSpan _readTimeout;
    private TimeSpan _writeTimeout;

    public TimedStream(Stream innerStream, TimeSpan readTimeout, TimeSpan writeTimeout)
    {
        _innerStream = innerStream;
        _readTimeout = readTimeout;
        _writeTimeout = writeTimeout;
        if (readTimeout != Timeout.InfiniteTimeSpan && writeTimeout != Timeout.InfiniteTimeSpan)
        {
            return;
        }

        _timeoutCts = new CancellationTokenSource();
    }

    public override bool CanTimeout => true;

    public override int ReadTimeout
    {
        get => (int)_readTimeout.TotalMilliseconds;
        set => _readTimeout = TimeSpan.FromMilliseconds(value);
    }

    public override int WriteTimeout
    {
        get => (int)_writeTimeout.TotalMilliseconds;
        set => _writeTimeout = TimeSpan.FromMilliseconds(value);
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

    public async override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        using TimeoutScope timeout = TimeoutAfter(_readTimeout, cancellationToken);
        await _innerStream.CopyToAsync(destination, bufferSize, timeout.Token);
    }

    public async override Task FlushAsync(CancellationToken cancellationToken)
    {
        using TimeoutScope timeout = TimeoutAfter(_writeTimeout, cancellationToken);
        await _innerStream.FlushAsync(timeout.Token);
    }

    public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        using TimeoutScope timeout = TimeoutAfter(_readTimeout, cancellationToken);
        return await _innerStream.ReadAsync(buffer.AsMemory(offset, count), timeout.Token);
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using TimeoutScope timeout = TimeoutAfter(_readTimeout, cancellationToken);
        return await _innerStream.ReadAsync(buffer, timeout.Token);
    }

    public async override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        using TimeoutScope timeout = TimeoutAfter(_writeTimeout, cancellationToken);
        await _innerStream.WriteAsync(buffer.AsMemory(offset, count), timeout.Token);
    }

    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using TimeoutScope timeout = TimeoutAfter(_writeTimeout, cancellationToken);
        await _innerStream.WriteAsync(buffer, timeout.Token);
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

    private TimeoutScope TimeoutAfter(TimeSpan timeSpan, CancellationToken cancellationToken) => new(this, timeSpan, cancellationToken);

    private sealed class TimeoutScope : IDisposable
    {
        private readonly TimedStream _stream;
        private readonly CancellationTokenSource? _combinedCts;
        private readonly CancellationToken _innerToken;
        private bool _disposedValue;

        public TimeoutScope(TimedStream stream, TimeSpan ioTimeout, CancellationToken cancellationToken)
        {
            _stream = stream;
            _innerToken = cancellationToken;
            if (_stream._timeoutCts == null && ioTimeout != Timeout.InfiniteTimeSpan)
            {
                return;
            }

            bool? canReuse = _stream._timeoutCts?.TryReset();
            if (canReuse is not true)
            {
                _stream._timeoutCts?.Dispose();
                _stream._timeoutCts = new CancellationTokenSource();
            }
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stream._timeoutCts!.Token);
            _stream._timeoutCts.CancelAfter(ioTimeout);
        }

        public CancellationToken Token
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposedValue, this);
                CancellationTokenSource? combinedCts = _combinedCts;
                return combinedCts?.Token ?? _innerToken;
            }
        }

        public void Dispose()
        {
            if (_disposedValue)
            {
                return;
            }

            _combinedCts?.Dispose();
            _stream._timeoutCts?.TryReset();
            _disposedValue = true;
        }
    }
}
