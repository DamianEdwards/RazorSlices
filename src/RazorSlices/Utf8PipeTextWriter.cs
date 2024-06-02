using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace Microsoft.AspNetCore.Internal;

// Adapted from https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/common/Shared/Utf8BufferTextWriter.cs
internal sealed class Utf8PipeTextWriter : TextWriter
{
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private const int MaximumBytesPerUtf8Char = 4;

    [ThreadStatic]
    private static Utf8PipeTextWriter? _cachedInstance;

    private PipeWriter? _pipeWriter;
    private Memory<byte> _memory;
    private int _memoryUsed;

#if DEBUG
    private bool _inUse;
#endif

    public override Encoding Encoding => _utf8NoBom;

    public static Utf8PipeTextWriter Get(PipeWriter pipeWriter)
    {
        var writer = _cachedInstance;
        writer ??= new Utf8PipeTextWriter();

        // Taken off the thread static
        _cachedInstance = null;
#if DEBUG
        if (writer._inUse)
        {
            throw new InvalidOperationException("The writer wasn't returned!");
        }

        writer._inUse = true;
#endif
        writer.SetWriter(pipeWriter);
        return writer;
    }

    public static void Return(Utf8PipeTextWriter writer)
    {
        _cachedInstance = writer;

        writer._memory = Memory<byte>.Empty;
        writer._memoryUsed = 0;
        writer._pipeWriter = null;

#if DEBUG
        writer._inUse = false;
#endif
    }

    [MemberNotNull(nameof(_pipeWriter))]
    public void SetWriter(PipeWriter pipeWriter)
    {
        _pipeWriter = pipeWriter;
    }

    public override void Write(char[] buffer, int index, int count)
    {
        WriteInternal(buffer.AsSpan(index, count));
    }

    public override void Write(char[]? buffer)
    {
        if (buffer is not null)
        {
            WriteInternal(buffer);
        }
    }

    public override void Write(char value)
    {
        if (value <= 127)
        {
            EnsureBuffer();

            // Only need to set one byte
            // Avoid Memory<T>.Slice overhead for perf
            _memory.Span[_memoryUsed] = (byte)value;
            _memoryUsed++;
        }
        else
        {
            WriteMultiByteChar(value);
        }
    }

    private void WriteMultiByteChar(char value)
    {
        var destination = GetBuffer();

        // Json.NET only writes ASCII characters by themselves, e.g. {}[], etc
        // this should be an exceptional case
        Utf8.FromUtf16(new ReadOnlySpan<char>(ref value), destination, out var charsUsed, out var bytesUsed);

        Debug.Assert(charsUsed == 1);

        _memoryUsed += bytesUsed;
    }

    public override void Write(string? value)
    {
        if (value is not null)
        {
            WriteInternal(value.AsSpan());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> GetBuffer()
    {
        EnsureBuffer();

        return _memory.Span[_memoryUsed.._memory.Length];
    }

    private void EnsureBuffer()
    {
        // We need at least enough bytes to encode a single UTF-8 character, or Encoder.Convert will throw.
        // Normally, if there isn't enough space to write every character of a char buffer, Encoder.Convert just
        // writes what it can. However, if it can't even write a single character, it throws. So if the buffer has only
        // 2 bytes left and the next character to write is 3 bytes in UTF-8, an exception is thrown.
        var remaining = _memory.Length - _memoryUsed;
        if (remaining < MaximumBytesPerUtf8Char)
        {
            // Used up the memory from the buffer writer so advance and get more
            if (_memoryUsed > 0)
            {
                _pipeWriter!.Advance(_memoryUsed);
            }

            _memory = _pipeWriter!.GetMemory(MaximumBytesPerUtf8Char);
            _memoryUsed = 0;
        }
    }

    private void WriteInternal(ReadOnlySpan<char> buffer)
    {
        while (buffer.Length > 0)
        {
            // The destination byte array might not be large enough so multiple writes are sometimes required
            var destination = GetBuffer();

            Utf8.FromUtf16(buffer, destination, out var charsUsed, out var bytesUsed);

            buffer = buffer[charsUsed..];
            _memoryUsed += bytesUsed;
        }
    }

    public override void Flush()
    {
        if (_memoryUsed > 0)
        {
            _pipeWriter!.Advance(_memoryUsed);
            _memory = _memory[_memoryUsed..];
            _memoryUsed = 0;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Flush();
        }
    }
}
