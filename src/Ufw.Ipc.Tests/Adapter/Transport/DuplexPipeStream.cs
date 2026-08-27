using System.Buffers;
using System.IO.Pipelines;

namespace Ufw.Ipc.Tests.Adapter.Transport;

/// <summary>
/// One half of an in-process full-duplex link backed by <see cref="Pipe"/>.
/// Portable: no OS IPC primitives are required.
/// </summary>
internal sealed class DuplexPipeStream : Stream
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly Action _completeLocalHalf;
    private bool _disposed;

    public DuplexPipeStream(PipeReader reader, PipeWriter writer, Action completeLocalHalf)
    {
        _reader = reader;
        _writer = writer;
        _completeLocalHalf = completeLocalHalf;
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => !_disposed;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() =>
        _writer.FlushAsync().AsTask().GetAwaiter().GetResult();

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        FlushResult result = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsCanceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.Length == 0)
        {
            return 0;
        }

        while (true)
        {
            ReadResult result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> sequence = result.Buffer;

            if (sequence.IsEmpty)
            {
                _reader.AdvanceTo(sequence.Start);
                if (result.IsCompleted)
                {
                    return 0;
                }

                if (result.IsCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException(cancellationToken);
                }

                continue;
            }

            // Only examine bytes we actually copy. Marking the entire buffer as examined while
            // consuming a single byte can prevent subsequent 1-byte reads from observing the
            // remainder (the framing stack reads a byte at a time via ReadAtLeastAsync).
            int toCopy = (int)Math.Min(sequence.Length, buffer.Length);
            ReadOnlySequence<byte> slice = sequence.Slice(0, toCopy);
            slice.CopyTo(buffer.Span);
            SequencePosition consumed = sequence.GetPosition(toCopy);
            _reader.AdvanceTo(consumed, consumed);
            return toCopy;
        }
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty)
        {
            return;
        }

        FlushResult result = await _writer.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (result.IsCanceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }

        if (result.IsCompleted)
        {
            throw new IOException("The remote half of the in-process duplex stream has been closed.");
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _completeLocalHalf();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _completeLocalHalf();
        _disposed = true;
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
