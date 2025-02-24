// Adapted from System.IO.StringWriter
// https://github.com/dotnet/runtime/blob/57ab984bd0dbbacd02315d41e09144d2823e9475/src/libraries/System.Private.CoreLib/src/System/IO/StringWriter.cs

using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace RazorSlices;

internal sealed class ReusableStringWriter : TextWriter
{
    private static UnicodeEncoding? _encoding;

    private StringBuilder? _sb;

    public ReusableStringWriter() { }

    public override Encoding Encoding => _encoding ??= new UnicodeEncoding(false, false);

    public void SetStringBuilder(StringBuilder sb)
    {
        _sb = sb;
    }

    public void Reset()
    {
        _sb = null;
    }

    public override void Write(char value)
    {
        _sb?.Append(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (buffer.Length - index < count)
        {
            throw new ArgumentException("Out of range");
        }

        _sb?.Append(buffer, index, count);
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        _sb?.Append(buffer);
    }

    public override void Write(string? value)
    {
        if (value is not null)
        {
            _sb?.Append(value);
        }
    }

    public override void Write(StringBuilder? value)
    {
        _sb?.Append(value);
    }

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        _sb?.Append(buffer);
        WriteLine();
    }

    public override void WriteLine(StringBuilder? value)
    {
        _sb?.Append(value);
        WriteLine();
    }

    #region Task based Async APIs

    public override Task WriteAsync(char value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(string? value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(char[] buffer, int index, int count)
    {
        Write(buffer, index, count);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Write(buffer.Span);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        _sb?.Append(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(char value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        _sb?.Append(value);
        WriteLine();
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(char[] buffer, int index, int count)
    {
        WriteLine(buffer, index, count);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        WriteLine(buffer.Span);
        return Task.CompletedTask;
    }

    public override Task FlushAsync()
    {
        return Task.CompletedTask;
    }

    #endregion
}

internal static class ReusableStringWriterObjectPoolProviderExtensions
{
    public static ObjectPool<ReusableStringWriter> CreateStringWriterPool(this ObjectPoolProvider poolProvider)
    {
        return poolProvider.Create(new ReusableStringWriterPooledObjectPolicy());
    }

    private class ReusableStringWriterPooledObjectPolicy : IPooledObjectPolicy<ReusableStringWriter>
    {
        public ReusableStringWriter Create() => new();

        public bool Return(ReusableStringWriter obj)
        {
            obj.Reset();
            return true;
        }
    }
}
