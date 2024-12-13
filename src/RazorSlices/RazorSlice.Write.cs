using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Buffers;
using System.Globalization;

namespace RazorSlices;

public partial class RazorSlice
{
    /// <summary>
    /// Writes a string value to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        _pipeWriter?.WriteHtml(value.AsSpan());
        _textWriter?.Write(value);
    }

    /// <summary>
    /// Writes the string representation of the provided object to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral<T>(T? value)
    {
        if (value is IUtf8SpanFormattable)
        {
            WriteUtf8SpanFormattable((IUtf8SpanFormattable)(object)value, htmlEncode: false);
            return;
        }

        if (value is ISpanFormattable)
        {
            WriteSpanFormattable((ISpanFormattable)(object)value, htmlEncode: false);
            return;
        }

        WriteLiteral(value?.ToString());
    }

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all blocks of HTML in your .cshtml file.
    /// </para>
    /// <para>
    /// NOTE: We'd need a tweak to the Razor compiler to to have it support emitting <see cref="WriteLiteral(ReadOnlySpan{byte})"/> calls with UTF8 string literals
    ///       i.e. https://github.com/dotnet/razor/issues/8429
    /// </para>
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void WriteLiteral(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _pipeWriter?.Write(value);
        _textWriter?.WriteUtf8(value);
    }

    /// <summary>
    /// Writes a <see cref="bool"/> value to the output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a value, use <see cref="WriteBool"/> instead, e.g. <c>@WriteBool(todo.Complete)</c>
    /// </para>
    /// </remarks>
    /// <param name="value"></param>
    protected void Write(bool? value) => WriteBool(value);

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output after HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(byte[] value) => Write(value.AsSpan());

    /// <summary>
    /// Writes a buffer of UTF8 bytes to the output after HTML encoding it.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _pipeWriter?.HtmlEncodeAndWriteUtf8(value, _htmlEncoder);
        _textWriter?.HtmlEncodeAndWriteUtf8(value, _htmlEncoder);
    }

    /// <summary>
    /// Writes the specified <see cref="HtmlString"/> value to the output without HTML encoding it again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all matching Razor expressions in your .cshtml file.
    /// </para>
    /// <para>
    /// To manually write out a value, use <see cref="WriteHtml{T}(T)"/> instead,
    /// e.g. <c>@WriteHtml(myCustomHtmlString)</c>
    /// </para>
    /// </remarks>
    /// <param name="htmlString">The <see cref="HtmlString"/> value to write to the output.</param>
    protected void Write(HtmlString htmlString)
    {
        if (htmlString is not null && htmlString != HtmlString.Empty)
        {
            WriteHtml(htmlString);
        }
    }

    /// <summary>
    /// Writes the specified <see cref="IHtmlContent"/> value to the output without HTML encoding it again.
    /// </summary>
    /// <param name="htmlContent">The <see cref="IHtmlContent"/> value to write to the output.</param>
    protected void Write(IHtmlContent? htmlContent)
    {
        if (htmlContent is not null)
        {
            WriteHtml(htmlContent);
        }
    }

    /// <summary>
    /// Writes the specified value to the output after HTML encoding it.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Write(value.AsSpan());
        }
    }

    /// <summary>
    /// Writes the specified value to the output after HTML encoding it.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    protected void Write(ReadOnlySpan<char> value)
    {
        if (value.Length > 0)
        {
            _pipeWriter?.HtmlEncodeAndWrite(value, _htmlEncoder);
            _textWriter?.HtmlEncodeAndWrite(value, _htmlEncoder);
        }
    }

    /// <summary>
    /// Writes the specified <typeparamref name="T"/> to the output.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit calls to the most appropriate overload of
    /// the <c>Write</c> method for all Razor expressions in your .cshtml file, e.g. <c>@someVariable</c>.
    /// </remarks>
    /// <param name="value">The <typeparamref name="T"/> to write to the output.</param>
    protected void Write<T>(T? value)
    {
        WriteValue(value);
    }

    /// <summary>
    /// Writes a <see cref="bool"/> value to the output.
    /// </summary>
    /// <param name="value">The value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteBool(todo.Completed)</c></returns>
    protected HtmlString WriteBool(bool? value)
    {
        if (value.HasValue)
        {
            _pipeWriter?.Write(value.Value);
            _textWriter?.Write(value.Value);
        }
        return HtmlString.Empty;
    }

    /// <summary>
    /// Write the specified <see cref="ISpanFormattable"/> value to the output with the specified format and optional <see cref="IFormatProvider" />.
    /// </summary>
    /// <param name="formattable">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteSpanFormattable(item.DueBy, "d")</c></returns>
    protected HtmlString WriteSpanFormattable<T>(T? formattable, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
        where T : ISpanFormattable
    {
        if (formattable is not null)
        {
            var htmlEncoder = htmlEncode ? _htmlEncoder : NullHtmlEncoder.Default;
            _pipeWriter?.HtmlEncodeAndWriteSpanFormattable(formattable, htmlEncoder, format, formatProvider);
            _textWriter?.HtmlEncodeAndWriteSpanFormattable(formattable, htmlEncoder, format, formatProvider);
        }

        return HtmlString.Empty;
    }

    /// <summary>
    /// Write the specified <see cref="IUtf8SpanFormattable"/> value to the output with the specified format and optional <see cref="IFormatProvider" />.
    /// </summary>
    /// <param name="formattable">The value to write to the output.</param>
    /// <param name="format">The format to use when writing the value to the output. Defaults to the default format for the value's type if not provided.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider" /> to use when writing the value to the output. Defaults to <see cref="CultureInfo.CurrentCulture"/> if <c>null</c>.</param>
    /// <param name="htmlEncode">Whether to HTML encode the value or not. Defaults to <c>true</c>.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteUtf8SpanFormattable(item.DueBy, "d")</c></returns>
    protected HtmlString WriteUtf8SpanFormattable<T>(T? formattable, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null, bool htmlEncode = true)
        where T : IUtf8SpanFormattable
    {
        if (formattable is not null)
        {
            var htmlEncoder = htmlEncode ? _htmlEncoder : NullHtmlEncoder.Default;
            _pipeWriter?.HtmlEncodeAndWriteUtf8SpanFormattable(formattable, htmlEncoder, format, formatProvider);
            _textWriter?.HtmlEncodeAndWriteUtf8SpanFormattable(formattable, htmlEncoder, format, formatProvider);
        }

        return HtmlString.Empty;
    }

    private static bool IsTaskFromAsyncMethod(Task task)
    {
        return task.GetType().FullName is { } fullName && fullName.StartsWith(nameof(System.Runtime.CompilerServices.AsyncTaskMethodBuilder), StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes the specified <see cref="IHtmlContent"/> value to the output.
    /// </summary>
    /// <param name="htmlContent">The <see cref="IHtmlContent"/> value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteHtmlContent(myCustomHtmlContent)</c></returns>
    protected HtmlString WriteHtml<T>(T htmlContent)
        where T : IHtmlContent
    {
#pragma warning disable CA2000 // Dispose objects before losing scope: Utf8PipeTextWriter is returned to the pool in the finally block
        TextWriter textWriter = _textWriter ?? Utf8PipeTextWriter.Get(_pipeWriter!);
#pragma warning restore CA2000
        var faulted = false;

        try
        {
            if (htmlContent is HelperResult helperResult)
            {
                // A templated Razor delegate is being rendered: https://learn.microsoft.com/aspnet/core/mvc/views/razor#templated-razor-delegates
                // HelperResult captures the generated async templated delegate and blocks synchronously when calling it!
                // This is not ideal for performance and in our case breaks the optimization used by Utf8PipeTextWriter which
                // is cached in a thread static, but it can't be helped without changing the Razor compiler (writes are synchronous).
                // However we can access the captured delegate, invoke it to get the Task and detect the case where it hasn't
                // completed (i.e. has gone async) and in that case throw an exception.
                var actionResult = helperResult.WriteAction(textWriter);

#if DEBUG
                // Force a small delay when debugging to make it easier to create scenario where method is async but has already completed
                Thread.Sleep(20);
#endif

                // If the Task is not completed or it's from a generated async method (i.e. one with an 'await' in it) throw an exception
                if (!actionResult.IsCompleted || IsTaskFromAsyncMethod(actionResult))
                {
                    // NOTE: There's still a chance here that the Task run asynchronously but is completed by the time we check it (albeit it's a small window)
                    //       and in that case it's very likely the Utf8PipeTextWriter will fault as it can't handle cross-thread writes (pooled via thread static).
                    //       I don't think this causes any issues as the exception will be thrown and the request will fail, but it's worth noting.

                    throw new InvalidOperationException("""
                        ----------------------------
                        !!! Razor Slices Error !!!
                        ----------------------------
                        The WriteAction of a HelperResult instance returned a Task from an async templated Razor delegate.
                        This causes performance and scale issues and is not supported in Razor Slices.
                        This happens when a templated Razor delegate does async work (i.e. has `@await SomethingAsync()` in it).
                        Use async templated methods instead of async templated Razor delegates. They have the advantage of supporting
                        regular method features too like generics and multiple parameters!

                        Do this:

                        ```
                        <div>
                            @await TemplatedMethod(DateTime.Now);
                        </div>

                        @functions {
                            private async Task<HtmlContent> TemplatedMethod<T>(T data, IHtmlContent? htmlPrefix = null)
                            {
                                @htmlPrefix
                                <p>
                                    @await SomeAsyncThing();
                                    The following data was passed: @data
                                </p>

                                // Returning HtmlContent.Empty makes it possible to call this using a Razor expression instead of a block
                                return HtmlContent.Empty;
                            }
                        }
                        ```

                        Instead of doing this:

                        ```
                        @{
                            Func<object, HelperResult> templatedRazorDelegate = @<p>
                                @{ await SomeAsyncThing(); }
                                Hello! The following value was passed: @item
                            </p>;
                        }

                        <div>
                            @templatedRazorDelegate(DateTime.Now)
                        </div>
                        ```
                        """);
                }

                actionResult.GetAwaiter().GetResult();
            }
            else
            {
                htmlContent?.WriteTo(textWriter, _htmlEncoder);
            }
        }
        catch
        {
            faulted = true;
            throw;
        }
        finally
        {
            if (textWriter is Utf8PipeTextWriter utf8PipeTextWriter)
            {
                if (!faulted)
                {
                    utf8PipeTextWriter.Flush();
                }
                Utf8PipeTextWriter.Return(utf8PipeTextWriter);
            }
        }

        return HtmlString.Empty;
    }

    /// <summary>
    /// Writes the specified <see cref="HtmlString"/> value to the output.
    /// </summary>
    /// <param name="htmlString">The <see cref="HtmlString"/> value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteHtml(myCustomHtmlContent)</c></returns>
    protected HtmlString WriteHtml(HtmlString htmlString)
    {
        if (htmlString is not null && htmlString != HtmlString.Empty)
        {
            _pipeWriter?.WriteHtml(htmlString.Value);
            _textWriter?.Write(htmlString.Value);
        }

        return HtmlString.Empty;
    }


    /// <summary>
    /// Writes the specified HTML string value to the output without HTML encoding it.
    /// </summary>
    /// <remarks>
    /// WARNING: This method does not HTML encode the value. Only call this method when you intend for the value to not be HTML encoded.
    /// </remarks>
    /// <param name="htmlString">The HTML value to write to the output.</param>
    /// <returns><see cref="HtmlString.Empty"/> to allow for easy calling via a Razor expression, e.g. <c>@WriteHtml(Model.SomeHtml)</c></returns>
    protected HtmlString WriteHtml(string? htmlString)
    {
        if (string.IsNullOrEmpty(htmlString))
        {
            _pipeWriter?.WriteHtml(htmlString.AsSpan());
            _textWriter?.Write(htmlString);
        }

        return HtmlString.Empty;
    }

    private void WriteValue<T>(T value)
    {
        if (value is null)
        {
            return;
        }

        // Dispatch to the most appropriately typed method
        if (TryWriteFormattableValue(value))
        {
            return;
        }

        if (value is string)
        {
            Write((string)(object)value);
        }
        else if (value is byte[])
        {
            Write(((byte[])(object)value).AsSpan());
        }
        // Handle derived types (this currently results in value types being boxed)
        else if (value is IUtf8SpanFormattable)
        {
            WriteUtf8SpanFormattable((IUtf8SpanFormattable)(object)value, default, null);
        }
        else if (value is ISpanFormattable)
        {
            WriteSpanFormattable((ISpanFormattable)(object)value, default, null);
        }
        else if (value is HtmlString)
        {
            WriteHtml((HtmlString)(object)value);
        }
        else if (value is IHtmlContent)
        {
            WriteHtml((IHtmlContent)(object)value);
        }
        else if (value is Enum)
        {
            WriteSpanFormattable((Enum)(object)value);
        }
        // Fallback to ToString()
        else
        {
            Write(value?.ToString());
        }
    }
}
