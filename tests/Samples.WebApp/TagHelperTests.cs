using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SliceTags = RazorSlices.Samples.WebApp.Slices.TagHelpers;

namespace RazorSlices.Samples.WebApp.Tests;

public class TagHelperTests
{
    [Fact]
    public async Task SdkDiscoversCodeAndTemplateAdaptersOnTheExistingSliceBase()
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var html = await client.GetStringAsync("/tag-helpers");
        Assert.Contains("Streaming tag helpers", html);
        Assert.Contains("Code-based &lt;unsafe&amp;&gt;", html);
        Assert.Contains("data-note=\"tag &lt;unsafe&amp;&gt;\"", html);
        Assert.Contains("""<span class="badge text-bg-success">42</span>""", html);
        Assert.Contains("<h2>Template-based &lt;unsafe&amp;&gt;</h2>", html);
        Assert.Contains("A shared Razor helper", html);
        Assert.Contains("Asynchronous body rendered once.", html);
        Assert.Contains("""<p class="minimal &lt;unsafe&amp;&gt;">&lt;unsafe&amp;&gt;</p>""", html);
        Assert.DoesNotContain("<rs:", html);
        Assert.DoesNotContain("hidden=", html);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BuiltInPartialTagStreamsFullAndMinimalSlices(bool minimal, bool pipe)
    {
        using var services = new ServiceCollection().AddRazorSlicesTagHelpers().BuildServiceProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> wait = async () =>
        {
            entered.SetResult();
            await release.Task;
        };
        RazorSliceBase partial = minimal ? new StreamingMinimalPartial(wait) : new StreamingFullPartial(wait);
        var slice = SliceTags.PartialProbe.Create(partial);
        slice.ServiceProvider = services;
        using var text = new StringWriter();
        var output = new Pipe();
        var render = pipe ? slice.RenderAsync(output.Writer).AsTask() : slice.RenderAsync(text).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(render.IsCompleted);
            var prefix = text.ToString();
            if (pipe)
            {
                Assert.True(output.Reader.TryRead(out var result));
                prefix = Encoding.UTF8.GetString(result.Buffer.ToArray());
                output.Reader.AdvanceTo(result.Buffer.End);
            }
            Assert.Contains("<span>before partial</span>", prefix);
            Assert.Contains("&lt;partial&gt;", prefix);
            Assert.DoesNotContain("after partial", prefix);
            if (partial is RazorSlice full) { Assert.Same(services, full.ServiceProvider); }
            release.SetResult();
            await render.WaitAsync(TimeSpan.FromSeconds(5));
            var suffix = text.ToString();
            if (pipe)
            {
                await output.Writer.CompleteAsync();
                var result = await output.Reader.ReadAsync();
                suffix = Encoding.UTF8.GetString(result.Buffer.ToArray());
                output.Reader.AdvanceTo(result.Buffer.End);
            }
            Assert.Contains("<span>partial suffix</span>", suffix);
            Assert.Contains("<span>after partial</span>", suffix);
            Assert.DoesNotContain("<rs:", prefix + suffix);
        }
        finally
        {
            release.TrySetResult();
            await render;
            await output.Writer.CompleteAsync();
            await output.Reader.CompleteAsync();
        }
    }

    private sealed class StreamingFullPartial(Func<Task> wait) : RazorSlice
    {
        public override async Task ExecuteAsync()
        {
            Write("<partial>");
            await FlushAsync();
            await wait();
            WriteLiteral("<span>partial suffix</span>");
        }
    }

    private sealed class StreamingMinimalPartial(Func<Task> wait) : RazorSliceBase
    {
        public override async Task ExecuteAsync()
        {
            Write("<partial>");
            await FlushAsync();
            await wait();
            WriteLiteral("<span>partial suffix</span>");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplateBodiesStreamAndAttributeHelpersUseTheCallingWriter(bool pipe)
    {
        using var services = new ServiceCollection().AddRazorSlicesTagHelpers().BuildServiceProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slice = SliceTags.StreamingProbe.Create(async () =>
        {
            entered.SetResult();
            await release.Task;
        });
        slice.ServiceProvider = services;
        using var text = new StringWriter();
        var output = new Pipe();
        var render = pipe ? slice.RenderAsync(output.Writer).AsTask() : slice.RenderAsync(text).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(render.IsCompleted);
            string prefix;
            if (pipe)
            {
                Assert.True(output.Reader.TryRead(out var result));
                prefix = Encoding.UTF8.GetString(result.Buffer.ToArray());
                output.Reader.AdvanceTo(result.Buffer.End);
            }
            else { prefix = text.ToString(); }

            Assert.Contains("<span>prefix</span>", prefix);
            Assert.DoesNotContain("suffix", prefix);
            Assert.Contains("Captured &lt;span", Regex.Replace(prefix, @"\s+", " "));
            Assert.DoesNotContain("""<span class="badge""", prefix);
            release.SetResult();
            await render.WaitAsync(TimeSpan.FromSeconds(5));
            if (pipe)
            {
                await output.Writer.CompleteAsync();
                var remaining = await output.Reader.ReadAsync();
                var suffix = Encoding.UTF8.GetString(remaining.Buffer.ToArray());
                output.Reader.AdvanceTo(remaining.Buffer.End);
                Assert.Contains("<span>suffix</span>", suffix);
                Assert.Contains("</article>", suffix);
            }
            else
            {
                Assert.Contains("<span>suffix</span>", text.ToString());
                Assert.Contains("</article>", text.ToString());
            }
        }
        finally
        {
            release.TrySetResult();
            await render;
            await output.Writer.CompleteAsync();
            await output.Reader.CompleteAsync();
        }
    }

    [Fact]
    public async Task TemplateBodyFailurePropagatesAndDoesNotRenderClosingMarkup()
    {
        using var services = new ServiceCollection().AddRazorSlicesTagHelpers().BuildServiceProvider();
        var failure = new InvalidOperationException("body failed");
        var slice = SliceTags.StreamingProbe.Create(() => Task.FromException(failure));
        slice.ServiceProvider = services;
        using var writer = new StringWriter();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.RenderAsync(writer));
        Assert.Same(failure, error);
        Assert.Contains("<span>prefix</span>", writer.ToString());
        Assert.DoesNotContain("</article>", writer.ToString());
        Assert.DoesNotContain("<span>suffix</span>", writer.ToString());
    }
}
