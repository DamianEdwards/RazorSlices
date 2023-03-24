using System.Buffers;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace RazorSlices;

internal static class TextWriterHtmlExtensions
{
    public static void HtmlEncodeAndWriteSpanFormattable<T>(this TextWriter textWriter, T formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        if (formattable is null)
        {
            return;
        }

        if (TryHtmlEncodeAndWriteSpanFormattableSmall(textWriter, formattable, htmlEncoder, format, formatProvider))
        {
            return;
        }

        var bufferSize = BufferSizes.MaxBufferSize;
        var formatBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        int charsWritten;

        while (!formattable.TryFormat(formatBuffer, out charsWritten, format, formatProvider))
        {
            // Buffer was too small, return the current buffer and rent a new buffer twice the size
            bufferSize = formatBuffer.Length * 2;
            ArrayPool<char>.Shared.Return(formatBuffer);
            formatBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        }

        var textToEncode = formatBuffer.AsSpan(..charsWritten);
        var encodeBuffer = ArrayPool<char>.Shared.Rent(BufferSizes.GetHtmlEncodedSizeHint(textToEncode.Length));
        var encodeBufferSpan = encodeBuffer.AsSpan();
        var encodeStatus = OperationStatus.Done;
        var waitingToWrite = 0;

        while (textToEncode.Length > 0)
        {
            if (encodeBufferSpan.Length == 0)
            {
                if (waitingToWrite > 0)
                {
                    textWriter.Write(encodeBuffer.AsSpan()[..waitingToWrite]);
                    waitingToWrite = 0;
                    encodeBufferSpan = encodeBuffer;
                }
            }

            encodeStatus = htmlEncoder.Encode(textToEncode, encodeBufferSpan, out var charsConsumed, out var charsEncoded);
            waitingToWrite += charsEncoded;

            if (textToEncode.Length - charsConsumed == 0)
            {
                break;
            }

            textToEncode = textToEncode[charsConsumed..];
            encodeBufferSpan = encodeBufferSpan[charsEncoded..];
        }

        if (waitingToWrite > 0)
        {
            textWriter.Write(encodeBufferSpan);
        }

        ArrayPool<char>.Shared.Return(encodeBuffer);
        ArrayPool<char>.Shared.Return(formatBuffer);

        Debug.Assert(encodeStatus == OperationStatus.Done, "Bad math in TextWriter HTML writing extensions");
    }

    private static bool TryHtmlEncodeAndWriteSpanFormattableSmall<T>(TextWriter textWriter, T formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        Span<char> formatBuffer = stackalloc char[BufferSizes.SmallFormattableWriteCharSize];
        if (formattable.TryFormat(formatBuffer, out var charsWritten, format, formatProvider))
        {
            if ((charsWritten * BufferSizes.HtmlEncodeAllowanceRatio) < BufferSizes.SmallFormattableWriteCharSize)
            {
                Span<char> encodedBuffer = stackalloc char[BufferSizes.SmallFormattableWriteCharSize];
                if (htmlEncoder.Encode(formatBuffer, encodedBuffer, out var charsConsumed, out var charsEncoded) == OperationStatus.Done)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
