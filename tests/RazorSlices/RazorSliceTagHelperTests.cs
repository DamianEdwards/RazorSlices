using System.Reflection;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.Runtime.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RazorSlices.Tests;

public class RazorSliceTagHelperTests
{
    [Fact]
    public async Task NestedAndRepeatedTagsStreamTheirBodiesOnce()
    {
        var bodyCalls = 0;
        var slice = new Host(async host =>
        {
            for (var i = 0; i < 2; i++)
            {
                await host.TagAsync(Wrap, async () =>
                {
                    await host.TagAsync(Wrap, () =>
                    {
                        bodyCalls++;
                        host.Literal("body");
                        return Task.CompletedTask;
                    });
                });
            }
        });
        Assert.Equal("<div><div>body</div></div><div><div>body</div></div>", await slice.RenderAsync());
        Assert.Equal(2, bodyCalls);
    }

    [Fact]
    public async Task SuppressionDoesNotExecuteTheInputBody()
    {
        var slice = new Host(host => host.TagAsync(
            static _ => ValueTask.CompletedTask,
            static () => throw new InvalidOperationException("Body must not run")));
        Assert.Equal("", await slice.RenderAsync());
    }

    [Fact]
    public async Task BodyReplayFailsExplicitlyAndCompletesTheContext()
    {
        RazorSliceTagHelperContext? captured = null;
        var calls = 0;
        var slice = new Host(host => host.TagAsync(async context =>
        {
            captured = context;
            await context.RenderBodyAsync();
            await context.RenderBodyAsync();
        }, () =>
        {
            calls++;
            return Task.CompletedTask;
        }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(TextWriter.Null));
        Assert.Contains("once", error.Message);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => captured!.Write("late"));
        Assert.Throws<InvalidOperationException>(() => _ = captured!.Attributes);
    }

    [Fact]
    public async Task MultipleRenderingHelpersFailBeforeAnyOutput()
    {
        var called = false;
        var slice = new Host(host => host.DuplicateAsync(_ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(TextWriter.Null));
        Assert.Contains("Only one", error.Message);
        Assert.False(called);
    }

    [Fact]
    public async Task ConditionalAndDataAttributesPreserveSdkEncodingAndSuppression()
    {
        var slice = new Host(host => host.TagAsync(Wrap, static () => Task.CompletedTask, host.Attributes));
        Assert.Equal("""<div class="a &lt;&amp;&gt;" checked="checked" title="one &amp; two" data-value="data &lt;&amp;&gt;"></div>""",
            await slice.RenderAsync());
    }

    [Fact]
    public async Task AttributeFailureRestoresTheCallerWriterDuringDisposal()
    {
        using var writer = new StringWriter();
        Host? slice = null;
        slice = new Host(host =>
        {
            host.BeginCapture();
            host.Literal("not output");
            throw new InvalidOperationException("capture failed");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(writer));
        Assert.Same(writer, slice.CurrentWriter);
        Assert.Equal("", writer.ToString());
    }

    [Fact]
    public async Task CancelledRenderNeverInvokesTheHelperOrItsBody()
    {
        var called = false;
        var slice = new Host(host => host.TagAsync(_ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        }, static () => throw new Exception("Body must not run")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await slice.RenderAsync(TextWriter.Null, cancellationToken: new CancellationToken(true)));
        Assert.False(called);
    }

    [Fact]
    public void MinimalBaseRetainsCompileTimeErrorsAndNoNewTagHelperState()
    {
        foreach (var name in new[] { "CreateTagHelper", "StartTagHelperWritingScope", "EndTagHelperWritingScope" })
        {
            var minimal = typeof(RazorSliceBase).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True(minimal.GetCustomAttribute<ObsoleteAttribute>()!.IsError);
        }
        Assert.DoesNotContain(typeof(RazorSlice).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType.Name.Contains("TagHelper", StringComparison.Ordinal));
    }

#if DEBUG
    [Fact(Skip = "Debug tracing allocates; measure unused-feature rendering in Release.")]
#else
    [Fact]
#endif
    public async Task UnusedTagHelperSupportAllocatesOnlyTheSlice()
    {
        for (var i = 0; i < 1_000; i++) { await new Empty().RenderAsync(TextWriter.Null); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) { GC.KeepAlive(new Empty()); }
        var instances = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) { await new Empty().RenderAsync(TextWriter.Null); }
        Assert.Equal(instances, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static async ValueTask Wrap(RazorSliceTagHelperContext context)
    {
        context.WriteLiteral("<div");
        foreach (var attribute in context.Attributes)
        {
            context.WriteLiteral(" ");
            context.WriteAttribute(attribute);
        }
        context.WriteLiteral(">");
        await context.RenderBodyAsync();
        context.WriteLiteral("</div>");
    }

    public sealed class CallbackTagHelper : RazorSliceTagHelper
    {
        internal Func<RazorSliceTagHelperContext, ValueTask> Callback { get; set; } = null!;
        protected override ValueTask RenderAsync(RazorSliceTagHelperContext context) => Callback(context);
    }

    private sealed class Empty : RazorSlice
    {
        public override Task ExecuteAsync() => Task.CompletedTask;
    }

    private sealed class Host(Func<Host, Task> render) : RazorSlice
    {
        private readonly TagHelperRunner _runner = new();
        private TagHelperScopeManager? _manager;
        internal TextWriter? CurrentWriter => _textWriter;
        public override Task ExecuteAsync() => render(this);
        internal void Literal(string value) => WriteLiteral(value);
        internal void BeginCapture() => BeginWriteTagHelperAttribute();

        internal async Task TagAsync(Func<RazorSliceTagHelperContext, ValueTask> callback, Func<Task> body,
            Action<TagHelperExecutionContext>? attributes = null)
        {
            var manager = _manager ??= new(StartTagHelperWritingScope, EndTagHelperWritingScope);
            var context = manager.Begin("test", TagMode.StartTagAndEndTag, "test", body);
            var helper = CreateTagHelper<CallbackTagHelper>();
            helper.Callback = callback;
            context.Add(helper);
            attributes?.Invoke(context);
            await _runner.RunAsync(context);
            if (!context.Output.IsContentModified) { await context.SetOutputContentAsync(); }
            Write(context.Output);
            manager.End();
        }

        internal async Task DuplicateAsync(Func<RazorSliceTagHelperContext, ValueTask> callback)
        {
            var manager = _manager ??= new(StartTagHelperWritingScope, EndTagHelperWritingScope);
            var context = manager.Begin("test", TagMode.StartTagAndEndTag, "test", static () => Task.CompletedTask);
            for (var i = 0; i < 2; i++)
            {
                var helper = CreateTagHelper<CallbackTagHelper>();
                helper.Callback = callback;
                context.Add(helper);
            }
            await _runner.RunAsync(context);
        }

        internal void Attributes(TagHelperExecutionContext context)
        {
            BeginAddHtmlAttributeValues(context, "class", 2, HtmlAttributeValueStyle.DoubleQuotes);
            AddHtmlAttributeValue("", 0, "a", 0, 0, true);
            AddHtmlAttributeValue(" ", 0, "<&>", 0, 0, false);
            EndAddHtmlAttributeValues(context);
            foreach (var (name, value) in new (string, object?)[] { ("hidden", false), ("missing", null), ("checked", true) })
            {
                BeginAddHtmlAttributeValues(context, name, 1, HtmlAttributeValueStyle.DoubleQuotes);
                AddHtmlAttributeValue("", 0, value, 0, 0, false);
                EndAddHtmlAttributeValues(context);
            }
            BeginAddHtmlAttributeValues(context, "title", 1, HtmlAttributeValueStyle.DoubleQuotes);
            AddHtmlAttributeValue("", 0, new HtmlString("one &amp; two"), 0, 0, false);
            EndAddHtmlAttributeValues(context);
            BeginWriteTagHelperAttribute();
            WriteLiteral("data ");
            Write("<&>");
            context.AddHtmlAttribute("data-value", new RazorSliceTagHelperHtml().Raw(EndWriteTagHelperAttribute()), HtmlAttributeValueStyle.DoubleQuotes);
        }
    }
}
