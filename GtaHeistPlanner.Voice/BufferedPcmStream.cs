using System.Collections.Concurrent;

namespace GtaHeistPlanner.Voice;

internal sealed class BufferedPcmStream : Stream
{
    private readonly VoiceDiagnosticTrace _diagnostics;
    private readonly BlockingCollection<byte[]> _buffers = new(new ConcurrentQueue<byte[]>());
    private byte[]? _current;
    private int _currentOffset;

    public BufferedPcmStream(VoiceDiagnosticTrace diagnostics)
    {
        _diagnostics = diagnostics;
        _diagnostics.Record("Custom producer/consumer PCM stream created.");
    }

    public void WriteFrame(byte[] frame)
    {
        if (!_buffers.IsAddingCompleted && frame.Length > 0)
            _buffers.Add(frame);
    }

    public void Complete() => _buffers.CompleteAdding();

    public override int Read(byte[] buffer, int offset, int count)
    {
        _diagnostics.RecordOnce("stream-read", "BufferedPcmStream.Read called.");
        while (_current is null || _currentOffset >= _current.Length)
        {
            try
            {
                _current = _buffers.Take();
                _currentOffset = 0;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        var copied = Math.Min(count, _current.Length - _currentOffset);
        Buffer.BlockCopy(_current, _currentOffset, buffer, offset, copied);
        _currentOffset += copied;
        return copied;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Complete();
            _buffers.Dispose();
        }
        base.Dispose(disposing);
    }

    public override bool CanRead
    {
        get { _diagnostics.RecordOnce("stream-can-read", "BufferedPcmStream.CanRead queried; returning true."); return true; }
    }
    public override bool CanSeek
    {
        get { _diagnostics.RecordOnce("stream-can-seek", "BufferedPcmStream.CanSeek queried; returning false."); return false; }
    }
    public override bool CanWrite
    {
        get { _diagnostics.RecordOnce("stream-can-write", "BufferedPcmStream.CanWrite queried; returning false."); return false; }
    }
    public override long Length
    {
        get
        {
            _diagnostics.Record("BufferedPcmStream.Length getter called; operation is unsupported.");
            throw new NotSupportedException("BufferedPcmStream.Length getter is not supported for a live stream.");
        }
    }
    public override long Position
    {
        get
        {
            _diagnostics.Record("BufferedPcmStream.Position getter called; operation is unsupported.");
            throw new NotSupportedException("BufferedPcmStream.Position getter is not supported for a live stream.");
        }
        set
        {
            _diagnostics.Record("BufferedPcmStream.Position setter called; operation is unsupported.");
            throw new NotSupportedException("BufferedPcmStream.Position setter is not supported for a live stream.");
        }
    }
    public override void Flush() { }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _diagnostics.RecordOnce("stream-read-async-array", "BufferedPcmStream.ReadAsync(byte[]) called.");
        return base.ReadAsync(buffer, offset, count, cancellationToken);
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _diagnostics.RecordOnce("stream-read-async-memory", "BufferedPcmStream.ReadAsync(Memory<byte>) called.");
        return base.ReadAsync(buffer, cancellationToken);
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        _diagnostics.Record("BufferedPcmStream.Seek called; operation is unsupported.");
        throw new NotSupportedException("BufferedPcmStream.Seek is not supported for a live stream.");
    }
    public override void SetLength(long value)
    {
        _diagnostics.Record("BufferedPcmStream.SetLength called; operation is unsupported.");
        throw new NotSupportedException("BufferedPcmStream.SetLength is not supported for a live stream.");
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        _diagnostics.Record("BufferedPcmStream.Write called; operation is unsupported.");
        throw new NotSupportedException("BufferedPcmStream.Write is not supported for a read-only live stream.");
    }
}
