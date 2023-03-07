using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace System.Buffers;

internal static class BufferWriterHtmlExtensions
{
    private const int SmallWriteByteSize = 256;
    private const int SmallWriteCharSize = SmallWriteByteSize / 2;
    private const int MaxCharBufferSize = 1024;

    private static readonly ArrayPool<char> _bufferPool = ArrayPool<char>.Shared;

    public static void HtmlEncodeAndWrite(this IBufferWriter<byte> bufferWriter, string text) =>
        HtmlEncodeAndWrite(bufferWriter, text, HtmlEncoder.Default);

    public static void HtmlEncodeAndWrite(this IBufferWriter<byte> bufferWriter, string text, HtmlEncoder htmlEncoder)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textSpan = text.AsSpan();

        if (textSpan.Length <= SmallWriteCharSize)
        {
            HtmlEncodeAndWriteSmall(bufferWriter, textSpan, htmlEncoder);
            return;
        }

        char[]? rentedBuffer = null;
        Span<char> encodedBuffer = default;
        var encodeStatus = OperationStatus.Done;

        while (textSpan.Length > 0)
        {
            if (encodedBuffer.Length == 0)
            {
                if (rentedBuffer is not null)
                {
                    _bufferPool.Return(rentedBuffer);
                }

                // TODO: What size should this be, i.e. how much space to allow for HTML encoding the string
                var rentedSpanSize = Math.Min(MaxCharBufferSize, (int)Math.Round(textSpan.Length * 1.1));
                rentedBuffer = _bufferPool.Rent(rentedSpanSize);
                encodedBuffer = rentedBuffer;
            }

            // Encode to rented buffer
            encodeStatus = htmlEncoder.Encode(textSpan, encodedBuffer, out var charsConsumed, out var charsWritten);

            // Write encoded chars to the writer
            var encoded = encodedBuffer[..charsWritten];
            WriteHtml(bufferWriter, encoded);

            textSpan = textSpan[charsConsumed..];
            encodedBuffer = encodedBuffer[charsWritten..];
        }

        if (rentedBuffer is not null)
        {
            _bufferPool.Return(rentedBuffer);
        }

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }

    private static void HtmlEncodeAndWriteSmall(IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> textSpan, HtmlEncoder htmlEncoder)
    {
        Span<char> encodedBuffer = stackalloc char[SmallWriteCharSize];
        var encodeStatus = OperationStatus.Done;

        // It's possible for encoding to take multiple cycles if an unusually high number of chars need HTML encoding and/or the
        // the total number of chars is close to the SmallWriteCharSize limit.
        while (textSpan.Length > 0)
        {
            // Encode to buffer
            encodeStatus = htmlEncoder.Encode(textSpan, encodedBuffer, out var charsConsumed, out var charsWritten);

            Debug.Assert(encodeStatus != OperationStatus.NeedMoreData, "Not expecting to hit this case");

            // Write encoded chars to the writer
            var encoded = encodedBuffer[..charsWritten];
            WriteHtml(bufferWriter, encoded);

            textSpan = textSpan[charsConsumed..];
        }

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return;
        }

        WriteHtml(bufferWriter, encoded.AsSpan());
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> encoded)
    {
        Span<byte> writerSpan = default;

        var status = OperationStatus.Done;

        while (encoded.Length > 0)
        {
            if (writerSpan.Length == 0)
            {
                var spanLengthHint = Math.Min(encoded.Length, MaxCharBufferSize);
                writerSpan = bufferWriter.GetSpan(spanLengthHint);
            }

            status = Utf8.FromUtf16(encoded, writerSpan, out var charsWritten, out var bytesWritten);

            encoded = encoded[charsWritten..];
            writerSpan = writerSpan[bytesWritten..];
            bufferWriter.Advance(bytesWritten);
        }

        Debug.Assert(status == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }
}