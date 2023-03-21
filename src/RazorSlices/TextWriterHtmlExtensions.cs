using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;

namespace RazorSlices;

internal static class TextWriterHtmlExtensions
{
    public static void HtmlEncodeAndWrite(this TextWriter textWriter, ISpanFormattable formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default)
    {
        if (formattable is null)
        {
            return;
        }

        if (TryHtmlEncodeAndWriteSmall(textWriter, formattable, htmlEncoder, format))
        {
            return;
        }

        var bufferSize = BufferLimits.MaxBufferSize;
        var formatBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        int charsWritten;

        while (!formattable.TryFormat(formatBuffer, out charsWritten, format, CultureInfo.CurrentCulture))
        {
            bufferSize = formatBuffer.Length * 2;
            ArrayPool<char>.Shared.Return(formatBuffer);
            formatBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        }

        var textToEncode = formatBuffer.AsSpan(..charsWritten);
        var encodeBuffer = ArrayPool<char>.Shared.Rent((int)Math.Floor(bufferSize * 1.1));
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

    private static bool TryHtmlEncodeAndWriteSmall(TextWriter textWriter, ISpanFormattable formattable, HtmlEncoder htmlEncoder, ReadOnlySpan<char> format = default)
    {
        Span<char> formatBuffer = stackalloc char[BufferLimits.SmallWriteCharSize];
        if (formattable.TryFormat(formatBuffer, out var charsWritten, format, CultureInfo.CurrentCulture))
        {
            if ((charsWritten * 1.1) < BufferLimits.SmallWriteCharSize)
            {
                Span<char> encodedBuffer = stackalloc char[BufferLimits.SmallWriteCharSize];
                if (htmlEncoder.Encode(formatBuffer, encodedBuffer, out var charsConsumed, out var charsEncoded) == OperationStatus.Done)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
