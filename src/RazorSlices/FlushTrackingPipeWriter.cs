using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using Microsoft.Extensions.ObjectPool;

namespace RazorSlices;

internal class FlushTrackingPipeWriter : PipeWriter
{
    private static readonly ObjectPool<FlushTrackingPipeWriter> _pool = ObjectPool.Create<FlushTrackingPipeWriter>();
    private PipeWriter _innerPipeWriter;
    private int _unflushedBytes;

    [Obsolete("Exists for use by ObjectPool only. Call Create instead.", error: true)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
                               // This field is initialized by the Create method below.
    public FlushTrackingPipeWriter() { }
#pragma warning restore CS8618

    [MemberNotNull(nameof(_innerPipeWriter))]
    public static PipeWriter Create(PipeWriter innerPipeWriter)
    {
        if (innerPipeWriter.CanGetUnflushedBytes)
        {
            return innerPipeWriter;
        }

        var instance = _pool.Get();
        instance.Initialize(innerPipeWriter);
        return instance;
    }

    public static void Return(PipeWriter instance)
    {
        if (instance is FlushTrackingPipeWriter flushTrackingPipeWriter)
        {
            flushTrackingPipeWriter.Reset();
            _pool.Return(flushTrackingPipeWriter);
        }
    }

    private void Initialize(PipeWriter innerPipeWriter)
    {
        _innerPipeWriter = innerPipeWriter;
        _unflushedBytes = 0;
    }

    private void Reset()
    {
        _innerPipeWriter = null!;
    }

    public override bool CanGetUnflushedBytes => true;

    public override long UnflushedBytes => _unflushedBytes;

    public override void Advance(int bytes)
    {
        _innerPipeWriter.Advance(bytes);
        _unflushedBytes += bytes;
    }

    public override void CancelPendingFlush()
    {
        // REVIEW: What to do here?
        _innerPipeWriter.CancelPendingFlush();
    }

    public override void Complete(Exception? exception = null)
    {
        _innerPipeWriter.Complete(exception);
    }

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        var result = _innerPipeWriter.FlushAsync(cancellationToken);

        _unflushedBytes = 0;

        return result;
    }

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        return _innerPipeWriter.GetMemory(sizeHint);
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        return _innerPipeWriter.GetSpan(sizeHint);
    }
}
