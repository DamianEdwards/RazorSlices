using System.Buffers.Text;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Razor.TagHelpers;
using RazorSlices;

namespace System.Buffers;

internal static class PipeWriterHtmlExtensions
{
    public static void HtmlEncodeAndWriteUtf8(this PipeWriter pipeWriter, ReadOnlySpan<byte> utf8Text, HtmlEncoder htmlEncoder)
    {
        if (utf8Text.Length == 0)
        {
            return;
        }

        if (htmlEncoder == NullHtmlEncoder.Default)
        {
            // No HTML encoding required
            pipeWriter.Write(utf8Text);
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
                    pipeWriter.Advance(waitingToAdvance);
                    waitingToAdvance = 0;
                }
                // Allow space for HTML encoding the string
                var spanSizeHint = BufferSizes.GetHtmlEncodedSizeHint(utf8Text.Length);
                writerSpan = pipeWriter.GetSpan(spanSizeHint);
            }

            // Encode to buffer
            encodeStatus = htmlEncoder.EncodeUtf8(utf8Text, writerSpan, out var bytesConsumed, out var bytesWritten);

            if (bytesConsumed == 0 && encodeStatus == OperationStatus.DestinationTooSmall)
            {
                // The buffer is too small to encode the current text, so reset the buffer span to 0 and continue the loop
                writerSpan = default;
                continue;
            }

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
            pipeWriter.Advance(waitingToAdvance);
        }

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IpipeWriter HTML writing extensions");
    }

    public static void HtmlEncodeAndWriteSpanFormattable<T>(this PipeWriter pipeWriter, T? formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        if (formattable is null)
        {
            return;
        }

        if (TryHtmlEncodeAndWriteSpanFormattableSmall(pipeWriter, formattable, htmlEncoder, format, formatProvider))
        {
            return;
        }

        var bufferSize = BufferSizes.SmallFormattableWriteCharSize * 2;
        var rentedBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        int charsWritten;

        while (!formattable.TryFormat(rentedBuffer, out charsWritten, format, formatProvider))
        {
            // Buffer was too small, return the current buffer and rent a new buffer twice the size
            bufferSize = rentedBuffer.Length * 2;
            ArrayPool<char>.Shared.Return(rentedBuffer);
            rentedBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        }

        HtmlEncodeAndWrite(pipeWriter, rentedBuffer.AsSpan()[..charsWritten], htmlEncoder);
        ArrayPool<char>.Shared.Return(rentedBuffer);
    }

    private static bool TryHtmlEncodeAndWriteSpanFormattableSmall<T>(PipeWriter pipeWriter, T formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        Span<char> buffer = stackalloc char[BufferSizes.SmallFormattableWriteCharSize];
        if (formattable.TryFormat(buffer, out var charsWritten, format, formatProvider))
        {
            HtmlEncodeAndWrite(pipeWriter, buffer[..charsWritten], htmlEncoder);
            return true;
        }
        return false;
    }

    public static void HtmlEncodeAndWriteUtf8SpanFormattable<T>(this PipeWriter pipeWriter, T? formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : IUtf8SpanFormattable
    {
        if (formattable is null)
        {
            return;
        }

        if (TryHtmlEncodeAndWriteUtf8SpanFormattableSmall(pipeWriter, formattable, htmlEncoder, format, formatProvider))
        {
            return;
        }

        var bufferSize = BufferSizes.SmallFormattableWriteByteSize * 2;
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int bytesWritten;

        while (!formattable.TryFormat(rentedBuffer, out bytesWritten, format, formatProvider))
        {
            // Buffer was too small, return the current buffer and rent a new buffer twice the size
            bufferSize = rentedBuffer.Length * 2;
            ArrayPool<byte>.Shared.Return(rentedBuffer);
            rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        }

        HtmlEncodeAndWriteUtf8(pipeWriter, rentedBuffer.AsSpan()[..bytesWritten], htmlEncoder);
        ArrayPool<byte>.Shared.Return(rentedBuffer);
    }

    private static bool TryHtmlEncodeAndWriteUtf8SpanFormattableSmall<T>(PipeWriter pipeWriter, T formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : IUtf8SpanFormattable
    {
        Span<byte> buffer = stackalloc byte[BufferSizes.SmallFormattableWriteByteSize];
        if (formattable.TryFormat(buffer, out var bytesWritten, format, formatProvider))
        {
            HtmlEncodeAndWriteUtf8(pipeWriter, buffer[..bytesWritten], htmlEncoder);
            return true;
        }
        return false;
    }

    public static void HtmlEncodeAndWrite(this PipeWriter pipeWriter, ReadOnlySpan<char> textSpan, HtmlEncoder htmlEncoder)
    {
        if (textSpan.Length == 0)
        {
            return;
        }

        if (htmlEncoder == NullHtmlEncoder.Default)
        {
            // No HTML encoding required
            WriteHtml(pipeWriter, textSpan);
            return;
        }

        if (textSpan.Length <= BufferSizes.SmallTextWriteCharSize)
        {
            HtmlEncodeAndWriteSmall(pipeWriter, textSpan, htmlEncoder);
            return;
        }

        var sizeHint = BufferSizes.GetHtmlEncodedSizeHint(textSpan.Length);
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
                    WriteHtml(pipeWriter, rentedBuffer.AsSpan()[..waitingToWrite]);
                    waitingToWrite = 0;
                }
                bufferSpan = rentedBuffer;
            }

            // Encode to rented buffer
            encodeStatus = htmlEncoder.Encode(textSpan, bufferSpan, out var charsConsumed, out var charsWritten);

            // REVIEW: This routine needs some review for how it handles cases where the buffer is too small.

            if (charsConsumed == 0 && encodeStatus == OperationStatus.DestinationTooSmall)
            {
                // The buffer is too small to encode the current text, so reset the buffer span to 0 and continue the loop
                bufferSpan = default;
                continue;
            }

            waitingToWrite += charsWritten;

            if (textSpan.Length - charsConsumed == 0)
            {
                break;
            }

            textSpan = textSpan[charsConsumed..];
            bufferSpan = bufferSpan[charsWritten..];
        }

        if (waitingToWrite > 0)
        {
            WriteHtml(pipeWriter, rentedBuffer.AsSpan()[..waitingToWrite]);
        }

        ArrayPool<char>.Shared.Return(rentedBuffer);

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IpipeWriter HTML writing extensions");
    }

    private static void HtmlEncodeAndWriteSmall(PipeWriter pipeWriter, ReadOnlySpan<char> textSpan, HtmlEncoder htmlEncoder)
    {
        Span<char> encodedBuffer = stackalloc char[BufferSizes.SmallTextWriteCharSize];
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
            WriteHtml(pipeWriter, encoded);

            textSpan = textSpan[charsConsumed..];
        }

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in IpipeWriter HTML writing extensions");
    }

    public static void WriteHtml(this PipeWriter pipeWriter, ReadOnlySpan<char> html)
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
                    pipeWriter.Advance(waitingToAdvance);
                    waitingToAdvance = 0;
                }
                var spanLengthHint = Math.Min(html.Length, BufferSizes.MaxBufferSize);
                writerSpan = pipeWriter.GetSpan(spanLengthHint);
            }

            status = Utf8.FromUtf16(html, writerSpan, out var charsRead, out var bytesWritten);

            if (charsRead == 0 && status == OperationStatus.DestinationTooSmall)
            {
                // The buffer is too small to encode the current text, so reset the buffer span to 0 and continue the loop
                writerSpan = default;
                continue;
            }

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
            pipeWriter.Advance(waitingToAdvance);
        }

        Debug.Assert(status == OperationStatus.Done, "Bad math in IpipeWriter HTML writing extensions");
    }

    private static readonly int TrueStringLength = bool.TrueString.Length;
    private static readonly int FalseStringLength = bool.FalseString.Length;

    public static void Write(this PipeWriter pipeWriter, bool value)
    {
        var buffer = pipeWriter.GetSpan(value ? TrueStringLength : FalseStringLength);
        if (!Utf8Formatter.TryFormat(value, buffer, out var _))
        {
            throw new FormatException("Unexpectedly insufficient space in buffer to format bool value.");
        }
    }
}