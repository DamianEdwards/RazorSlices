using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Tests;

public class PipeWriterEncodingTests
{
    public static TheoryData<string> TextCases => new()
    {
        "",
        "-",
        "\u010D",
        "\u2013",
        "\u2013\r\n",
        "\U0001F600",
        "\U0001F600x",
        "\uD800",
        "\uDC00x",
        "<",
        "&",
        "<>&\"'",
        "a\u010D\u2013\U0001F600z",
        new string('&', 127),
        new string('&', 128),
        new string('&', 129),
        new string('a', 1023) + "\u2013",
        new string('a', 1024) + "\U0001F600",
        string.Concat(Enumerable.Repeat("\u010D\u2013\U0001F600<&", 300))
    };

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task WriteHtml_WritesExactUtf8AcrossSegmentBoundaries(string text)
    {
        for (var remaining = 0; remaining <= 12; remaining++)
        {
            await AssertPipeOutput(remaining, Encoding.UTF8.GetBytes(text), writer =>
            {
                writer.WriteHtml(text);
                return ValueTask.CompletedTask;
            });
        }
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task HtmlEncodeAndWrite_WritesExactOutputAcrossSegmentBoundaries(string text)
    {
        HtmlEncoder[] encoders = [HtmlEncoder.Default, HtmlEncoder.Create(UnicodeRanges.All), NullHtmlEncoder.Default];
        foreach (var encoder in encoders)
        {
            for (var remaining = 0; remaining <= 12; remaining++)
            {
                var expected = Encoding.UTF8.GetBytes(encoder.Encode(text));
                await AssertPipeOutput(remaining, expected, writer =>
                {
                    writer.HtmlEncodeAndWrite(text, encoder);
                    return ValueTask.CompletedTask;
                });
            }
        }
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task HtmlEncodeAndWriteUtf8_WritesExactOutputAcrossSegmentBoundaries(string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        HtmlEncoder[] encoders = [HtmlEncoder.Default, HtmlEncoder.Create(UnicodeRanges.All), NullHtmlEncoder.Default];
        foreach (var encoder in encoders)
        {
            for (var remaining = 0; remaining <= 12; remaining++)
            {
                var expected = Encoding.UTF8.GetBytes(encoder.Encode(Encoding.UTF8.GetString(utf8)));
                await AssertPipeOutput(remaining, expected, writer =>
                {
                    writer.HtmlEncodeAndWriteUtf8(utf8, encoder);
                    return ValueTask.CompletedTask;
                });
            }
        }
    }

    [Theory]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { 0xC4 })]
    [InlineData(new byte[] { 0xF0, 0x9F, 0x92 })]
    [InlineData(new byte[] { 0xE2, 0x28, 0xA1 })]
    public async Task HtmlEncodeAndWriteUtf8_ReplacesInvalidUtf8AcrossSegmentBoundaries(byte[] utf8)
    {
        var expected = Encoding.UTF8.GetBytes(HtmlEncoder.Default.Encode(Encoding.UTF8.GetString(utf8)));
        for (var remaining = 0; remaining <= 12; remaining++)
        {
            await AssertPipeOutput(remaining, expected, writer =>
            {
                writer.HtmlEncodeAndWriteUtf8(utf8, HtmlEncoder.Default);
                return ValueTask.CompletedTask;
            });
        }
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public async Task Utf8PipeTextWriter_WritesExactUtf8AcrossSegmentBoundaries(string text)
    {
        for (var remaining = 0; remaining <= 4; remaining++)
        {
            await AssertPipeOutput(remaining, Encoding.UTF8.GetBytes(text + text), writer =>
            {
                var textWriter = Utf8PipeTextWriter.Get(writer);
                try
                {
                    textWriter.Write(text);
                    textWriter.Flush();
                    textWriter.Write(text.AsSpan());
                    textWriter.Flush();
                }
                finally
                {
                    Utf8PipeTextWriter.Return(textWriter);
                }
                return ValueTask.CompletedTask;
            });
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RenderAsync_WritesUnicodeLiteralAndRawHtmlWithOneByteRemaining(int writeKind)
    {
        const string text = "\u2013";
        await AssertPipeOutput(1, Encoding.UTF8.GetBytes(text), writer =>
            new UnicodeSlice(text, writeKind).RenderAsync(writer));
    }

    private static async Task AssertPipeOutput(int remaining, byte[] expected, Func<PipeWriter, ValueTask> write)
    {
        await AssertPipeOutput(remaining, expected, write, exactSizeHints: false);
        await AssertPipeOutput(remaining, expected, write, exactSizeHints: true);
    }

    private static async Task AssertPipeOutput(int remaining, byte[] expected, Func<PipeWriter, ValueTask> write, bool exactSizeHints)
    {
        var pipe = new Pipe(new PipeOptions(minimumSegmentSize: 16, useSynchronizationContext: false));
        var prefixLength = FillSegment(pipe.Writer, remaining);
        var writer = new ProgressGuardPipeWriter(pipe.Writer, exactSizeHints);
        try
        {
            await write(writer);
            await writer.CompleteAsync();
            var result = await pipe.Reader.ReadAsync();
            var bytes = result.Buffer.ToArray();
            Assert.Equal(prefixLength + expected.Length, bytes.Length);
            Assert.All(bytes.Take(prefixLength), value => Assert.Equal((byte)'a', value));
            Assert.Equal(expected, bytes.AsSpan(prefixLength).ToArray());
            pipe.Reader.AdvanceTo(result.Buffer.End);
        }
        finally
        {
            await pipe.Writer.CompleteAsync();
            await pipe.Reader.CompleteAsync();
        }
    }

    private static int FillSegment(PipeWriter writer, int remaining)
    {
        var span = writer.GetSpan(16);
        var prefixLength = span.Length - remaining;
        span[..prefixLength].Fill((byte)'a');
        writer.Advance(prefixLength);
        return prefixLength;
    }

    // Bound retries so a regression fails instead of leaving a spinning test thread.
    private sealed class ProgressGuardPipeWriter(PipeWriter inner, bool exactSizeHints) : PipeWriter
    {
        private int _requestsWithoutAdvance;

        public override void Advance(int bytes)
        {
            inner.Advance(bytes);
            if (bytes > 0)
            {
                _requestsWithoutAdvance = 0;
            }
        }

        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            CheckProgress();
            var span = inner.GetSpan(sizeHint);
            return exactSizeHints ? span[..Math.Max(sizeHint, 1)] : span;
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            CheckProgress();
            var memory = inner.GetMemory(sizeHint);
            return exactSizeHints ? memory[..Math.Max(sizeHint, 1)] : memory;
        }

        private void CheckProgress()
        {
            if (++_requestsWithoutAdvance > 32)
            {
                throw new InvalidOperationException("Encoding repeatedly requested a buffer without advancing.");
            }
        }

        public override void CancelPendingFlush() => inner.CancelPendingFlush();
        public override void Complete(Exception? exception = null) => inner.Complete(exception);
        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => inner.FlushAsync(cancellationToken);
    }

    private sealed class UnicodeSlice(string text, int writeKind) : RazorSlice
    {
        public override Task ExecuteAsync()
        {
            switch (writeKind)
            {
                case 0:
                    WriteLiteral(text);
                    break;
                case 1:
                    WriteHtml(text);
                    break;
                case 2:
                    WriteHtml(new HtmlString(text));
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
