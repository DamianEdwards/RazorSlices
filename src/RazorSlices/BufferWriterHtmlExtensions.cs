using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace System.Buffers;

internal static class BufferWriterHtmlExtensions
{
    private const int SmallWriteByteSize = 256;
    private const int SmallWriteCharSize = SmallWriteByteSize / 2;
    private const int MaxBufferSize = 1024;

    public static void HtmlEncodeAndWriteUtf8(this IBufferWriter<byte> bufferWriter, ReadOnlySpan<byte> utf8Text) =>
        HtmlEncodeAndWriteUtf8(bufferWriter, utf8Text, HtmlEncoder.Default);

    public static void HtmlEncodeAndWriteUtf8(this IBufferWriter<byte> bufferWriter, ReadOnlySpan<byte> utf8Text, HtmlEncoder htmlEncoder)
    {
        if (utf8Text.Length == 0)
        {
            return;
        }

        Span<byte> writerSpan = default;
        var encodeStatus = OperationStatus.Done;
        var waitingToAdvance = 0;

        while (utf8Text.Length > 0)
        {
            if (writerSpan.Length == 0)
            {
                if (waitingToAdvance > 0)
                {
                    bufferWriter.Advance(waitingToAdvance);
                    waitingToAdvance = 0;
                }
                // TODO: What size should this be, i.e. how much space to allow for HTML encoding the string
                var spanSizeHint = GetEncodedSizeHint(utf8Text.Length);
                writerSpan = bufferWriter.GetSpan(spanSizeHint);
            }

            // Encode to rented buffer
            encodeStatus = htmlEncoder.EncodeUtf8(utf8Text, writerSpan, out var bytesConsumed, out var bytesWritten);
            waitingToAdvance += bytesWritten;

            if (utf8Text.Length - bytesConsumed == 0)
            {
                break;
            }

            utf8Text = utf8Text[bytesConsumed..];
            writerSpan = writerSpan[bytesWritten..];
        }

        if (waitingToAdvance > 0)
        {
            bufferWriter.Advance(waitingToAdvance);
        }

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }

    public static void HtmlEncodeAndWrite(this IBufferWriter<byte> bufferWriter, string? text) =>
        HtmlEncodeAndWrite(bufferWriter, text, HtmlEncoder.Default);

    public static void HtmlEncodeAndWrite(this IBufferWriter<byte> bufferWriter, string? text, HtmlEncoder htmlEncoder)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        HtmlEncodeAndWrite(bufferWriter, text.AsSpan(), htmlEncoder);
    }

    public static void HtmlEncodeAndWrite(this IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> textSpan, HtmlEncoder htmlEncoder)
    {
        if (textSpan.Length == 0)
        {
            return;
        }

        if (textSpan.Length <= SmallWriteCharSize)
        {
            HtmlEncodeAndWriteSmall(bufferWriter, textSpan, htmlEncoder);
            return;
        }

        var sizeHint = GetEncodedSizeHint(textSpan.Length);
        var rentedBuffer = ArrayPool<char>.Shared.Rent(sizeHint);
        Span<char> bufferSpan = rentedBuffer;
        var waitingToWrite = 0;
        var encodeStatus = OperationStatus.Done;

        while (textSpan.Length > 0)
        {
            if (bufferSpan.Length == 0)
            {
                if (waitingToWrite > 0)
                {
                    WriteHtml(bufferWriter, rentedBuffer.AsSpan()[..waitingToWrite]);
                    waitingToWrite = 0;
                }
            }

            // Encode to rented buffer
            encodeStatus = htmlEncoder.Encode(textSpan, bufferSpan, out var charsConsumed, out var charsWritten);
            waitingToWrite += charsWritten;

            if (textSpan.Length - charsConsumed == 0)
            {
                break;
            }

            textSpan = textSpan[charsConsumed..];
            bufferSpan = bufferSpan[charsWritten..];
        }

        if (rentedBuffer is not null)
        {
            if (waitingToWrite > 0)
            {
                WriteHtml(bufferWriter, rentedBuffer.AsSpan()[..waitingToWrite]);
            }
            ArrayPool<char>.Shared.Return(rentedBuffer);
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

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        WriteHtml(bufferWriter, html.AsSpan());
    }

    public static void WriteHtml(this IBufferWriter<byte> bufferWriter, ReadOnlySpan<char> html)
    {
        Span<byte> writerSpan = default;

        var status = OperationStatus.Done;
        int waitingToAdvance = 0;

        while (html.Length > 0)
        {
            if (writerSpan.Length == 0)
            {
                if (waitingToAdvance > 0)
                {
                    bufferWriter.Advance(waitingToAdvance);
                    waitingToAdvance = 0;
                }
                var spanLengthHint = Math.Min(html.Length, MaxBufferSize);
                writerSpan = bufferWriter.GetSpan(spanLengthHint);
            }

            status = Utf8.FromUtf16(html, writerSpan, out var charsRead, out var bytesWritten);
            waitingToAdvance += bytesWritten;

            if (html.Length - charsRead == 0)
            {
                break;
            }

            html = html[charsRead..];
            writerSpan = writerSpan[bytesWritten..];
        }

        if (waitingToAdvance > 0)
        {
            bufferWriter.Advance(waitingToAdvance);
        }

        Debug.Assert(status == OperationStatus.Done, "Bad math in IBufferWriter HTML writing extensions");
    }

    private static int GetEncodedSizeHint(int length)
    {
        return Math.Min(MaxBufferSize, (int)Math.Round(length * 1.1));
    }
}