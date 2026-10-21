using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorSlices.Tests;

public class RazorSliceHelperTreeTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddRazorSliceHelpers().BuildServiceProvider();

    public void Dispose()
    {
        _services.Dispose();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootNestedAndSiblingPartialsShareOneHelper(bool pipe)
    {
        var created = new List<Helper>();
        Helper Create()
        {
            var helper = new Helper();
            created.Add(helper);
            return helper;
        }

        var grandchild = CreateHost(async slice =>
        {
            var helper = slice.Get(Create);
            await Task.Yield();
            helper.Text("grandchild|");
        });
        var first = CreateHost(async slice =>
        {
            slice.Get(Create).Text("first|");
            await slice.Partial(grandchild);
            slice.Get(Create).Text("first-after|");
        });
        var sibling = CreateHost(slice =>
        {
            slice.Get(Create).Text("sibling|");
            return Task.CompletedTask;
        });
        var root = CreateHost(async slice =>
        {
            slice.Get(Create).Text("root|");
            await slice.Partial(first);
            Assert.Equal(1, first.Disposals);
            await slice.Partial(sibling);
            slice.Get(Create).Text("end");
        });

        Assert.Equal("root|first|grandchild|first-after|sibling|end", await Render(root, pipe));
        var shared = Assert.Single(created);
        Assert.Throws<InvalidOperationException>(() => shared.Text("after"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChildFirstUseSharesHelperAndRestoresParentWriterAfterFailure(bool pipe)
    {
        Helper? shared = null;
        using var nestedWriter = new StringWriter();
        var child = CreateHost(async slice =>
        {
            shared = slice.Get(static () => new());
            await slice.WithWriter(nestedWriter, async () =>
            {
                await Task.Yield();
                shared.Text("<\u00e9>");
            });
            throw new InvalidOperationException("child failed");
        });
        var root = CreateHost(async slice =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await slice.Partial(child));
            Assert.Same(shared, slice.Get(static () => throw new Exception("Second instance")));
            shared!.Text("parent");
        });

        Assert.Equal("parent", await Render(root, pipe));
        Assert.Equal("&lt;\u00e9&gt;", nestedWriter.ToString());
        Assert.Throws<InvalidOperationException>(() => shared!.Text("after"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CapturedHelperUsesActiveChildWriterAndRestoresRoot(bool pipe, bool asynchronous)
    {
        Helper? shared = null;
        using var childWriter = new StringWriter();
        var child = CreateHost(async slice =>
        {
            await slice.WithWriter(childWriter, async () =>
            {
                if (asynchronous)
                {
                    await Task.Yield();
                }

                shared!.Text("child");
            });
        });
        var root = CreateHost(async slice =>
        {
            shared = slice.Get(static () => new());
            shared.Text("before|");
            await slice.Partial(child);
            shared.Text("after");
        });

        Assert.Equal("before|after", await Render(root, pipe));
        Assert.Equal("child", childWriter.ToString());
        Assert.Throws<InvalidOperationException>(() => shared!.Text("disposed"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InitializationHelperUsesOutermostLayoutWriter(bool pipe, bool asynchronous)
    {
        Helper? shared = null;
        using var layoutWriter = new StringWriter();
        var outer = new OuterLayout(async layout =>
        {
            await layout.WithWriter(layoutWriter, async () =>
            {
                shared!.Text("before|");
                await layout.Body();
                shared.Text("after");
            });
        });
        var inner = new ModelLayout(async layout =>
        {
            shared!.Text("inner|");
            await layout.Body();
            shared.Text("inner-end|");
        }, () => outer)
        { Model = 42 };
        var content = new Content(async _ =>
        {
            if (asynchronous)
            {
                await Task.Yield();
            }

            shared!.Text("body|");
        }, static (_, _) => Task.CompletedTask, () => inner)
        {
            OnCreatingLayout = slice =>
            {
                shared = ((Host)slice).Get(static () => new());
                shared.Text("init|");
            }
        };

        Assert.Equal("init|", await Render(content, pipe));
        Assert.Equal("before|inner|body|inner-end|after", layoutWriter.ToString());
        Assert.Throws<InvalidOperationException>(() => shared!.Text("disposed"));
    }

#if DEBUG
    [Theory(Skip = "Debug render tracing allocates; run allocation checks in Release.")]
#else
    [Theory]
#endif
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    public async Task HelperFreeRenderingDoesNotAllocateBeyondSliceInstances(int depth)
    {
        const int iterations = 10_000;
        for (var i = 0; i < 1_000; i++)
        {
            var renderTask = new HelperFreeHost(depth).RenderAsync(TextWriter.Null);
            Assert.True(renderTask.IsCompletedSuccessfully);
            await renderTask;
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            for (var childDepth = depth; childDepth >= 0; childDepth--)
            {
                GC.KeepAlive(new HelperFreeHost(childDepth));
            }
        }
        var instanceAllocations = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var renderTask = new HelperFreeHost(depth).RenderAsync(TextWriter.Null);
            Assert.True(renderTask.IsCompletedSuccessfully);
            await renderTask;
        }
        var renderAllocations = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(instanceAllocations, renderAllocations);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NestedLayoutsBodiesAndSectionsShareInitializationHelperUntilRootFinishes(bool pipe, bool asynchronous)
    {
        var created = new List<Helper>();
        Helper Create()
        {
            var helper = new Helper();
            created.Add(helper);
            return helper;
        }
        var partial = CreateHost(async slice =>
        {
            if (asynchronous)
            {
                await Task.Yield();
            }

            slice.Get(Create).Text("partial|");
        });
        var outer = new OuterLayout(async layout =>
        {
            layout.Get(Create).Text("outer|");
            await layout.Body();
            layout.Get(Create).Text("outer-end");
        });
        var inner = new ModelLayout(async layout =>
        {
            layout.Get(Create).Text("inner|");
            await layout.Section("before");
            await layout.Body();
            await layout.Section("after");
            layout.Get(Create).Text("inner-end|");
        }, () => outer)
        { Model = 42 };
        var content = new Content(async slice =>
        {
            slice.Get(Create).Text("body|");
            await slice.Partial(partial);
        }, async (slice, name) =>
        {
            if (asynchronous)
            {
                await Task.Yield();
            }

            slice.Get(Create).Text(name + "|");
        }, () => inner)
        {
            OnCreatingLayout = slice =>
            {
                ((Content)slice).Get(Create).Text("init|");
            }
        };

        Assert.Equal("init|outer|inner|before|body|partial|after|inner-end|outer-end", await Render(content, pipe));
        var shared = Assert.Single(created);
        Assert.Equal(1, content.Disposals);
        Assert.Throws<InvalidOperationException>(() => shared.Text("after"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootFailureUnbindsHelperCreatedInDescendant(bool pipe)
    {
        Helper? shared = null;
        var child = CreateHost(async slice =>
        {
            shared = slice.Get(static () => new());
            await Task.Yield();
            throw new OperationCanceledException();
        });
        var root = CreateHost(async slice => await slice.Partial(child));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Render(root, pipe));
        Assert.Throws<InvalidOperationException>(() => shared!.Text("after"));
    }

    [Fact]
    public async Task IndependentRootsInSameServiceScopeShareHelpers()
    {
        var request = new DefaultHttpContext();
        Helper? first = null;
        Helper? second = null;
        var root = CreateHost(slice =>
        {
            first = slice.Get(static () => new());
            return Task.CompletedTask;
        });
        root.HttpContext = request;
        var other = CreateHost(slice =>
        {
            second = slice.Get(static () => new());
            return Task.CompletedTask;
        });
        other.HttpContext = request;

        await root.RenderAsync(TextWriter.Null);
        await other.RenderAsync(TextWriter.Null);
        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LayoutFailureUnbindsSharedHelper(bool sectionFailure)
    {
        Helper? shared = null;
        var layout = new OuterLayout(async slice =>
        {
            shared = slice.Get(static () => new());
            if (sectionFailure)
            {
                await slice.Section("failed");
            }
            else
            {
                await slice.Body();
            }
        });
        var content = new Content(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("body failed");
        }, async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("section failed");
        }, () => layout)
        {
            ServiceProvider = _services
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await content.RenderAsync(TextWriter.Null));
        Assert.Throws<InvalidOperationException>(() => shared!.Text("after"));
    }

    private Host CreateHost(Func<Host, Task> render)
    {
        return new(render) { ServiceProvider = _services };
    }

    private async Task<string> Render(RazorSlice slice, bool pipe)
    {
        slice.ServiceProvider = _services;
        var encoder = HtmlEncoder.Create(UnicodeRanges.All);
        if (!pipe)
        {
            return await slice.RenderAsync(encoder);
        }

        using var stream = new MemoryStream();
        var writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        try
        {
            await slice.RenderAsync(writer, encoder);
            await writer.CompleteAsync();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            await writer.CompleteAsync();
            throw;
        }
    }

    public interface ITextHelper
    {
        void Text(string text);
    }

    public sealed class Helper : RazorSliceHelpers, ITextHelper
    {
        public override Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }

        public void Text(string text)
        {
            Write(text);
        }
    }

    private sealed class HelperFreeHost(int depth) : RazorSlice
    {
        public override async Task ExecuteAsync()
        {
            if (depth > 0)
            {
                await RenderPartialAsync(new HelperFreeHost(depth - 1));
            }
        }
    }

    private class Host : RazorSlice
    {
        private readonly Func<Host, Task> _render;
        internal Host(Func<Host, Task> render)
        {
            _render = render;
            Activation = RazorSliceFactory.GetActivation(GetType());
        }
        [RazorInject]
        public IHelperManager Helpers { get; set; } = null!;
        internal int Disposals;

        internal Helper Get(Func<Helper> create)
        {
            return (Helper)Helpers.GetHelper<ITextHelper, Helper>(create);
        }

        internal ValueTask<Microsoft.AspNetCore.Html.HtmlString> Partial(RazorSlice child)
        {
            return RenderPartialAsync(child);
        }

        internal async Task WithWriter(TextWriter writer, Func<Task> renderContent)
        {
            PushWriter(writer);
            try
            {
                await renderContent();
            }
            finally
            {
                PopWriter();
            }
        }

        public override Task ExecuteAsync()
        {
            return _render(this);
        }

        public override void Dispose()
        {
            Disposals++;
            base.Dispose();
        }
    }

    private sealed class Content(Func<Host, Task> render, Func<Content, string, Task> section, Func<RazorSlice> layout)
        : Host(render), IUsesLayout<LayoutProxy>
    {
        internal Action<Content>? OnCreatingLayout { get; set; }
        public RazorSlice CreateLayout()
        {
            OnCreatingLayout?.Invoke(this);
            var slice = layout();
            slice.Activation = RazorSliceFactory.GetActivation(slice.GetType());
            return slice;
        }

        protected internal override Task ExecuteSectionAsync(string sectionName)
        {
            return section(this, sectionName);
        }
    }

    private sealed class OuterLayout(Func<OuterLayout, Task> render) : RazorLayoutSlice
    {
        [RazorInject]
        public IHelperManager Helpers { get; set; } = null!;
        internal Helper Get(Func<Helper> create)
        {
            return (Helper)Helpers.GetHelper<ITextHelper, Helper>(create);
        }

        internal ValueTask<Microsoft.AspNetCore.Html.HtmlString> Body()
        {
            return RenderBodyAsync();
        }

        internal ValueTask<Microsoft.AspNetCore.Html.HtmlString> Section(string name)
        {
            return RenderSectionAsync(name);
        }

        internal async Task WithWriter(TextWriter writer, Func<Task> renderContent)
        {
            PushWriter(writer);
            try
            {
                await renderContent();
            }
            finally
            {
                PopWriter();
            }
        }

        public override Task ExecuteAsync()
        {
            return render(this);
        }
    }

    private sealed class ModelLayout(Func<ModelLayout, Task> render, Func<RazorSlice> layout)
        : RazorLayoutSlice<int>, IUsesLayout<LayoutProxy>
    {
        [RazorInject]
        public IHelperManager Helpers { get; set; } = null!;
        internal Helper Get(Func<Helper> create)
        {
            return (Helper)Helpers.GetHelper<ITextHelper, Helper>(create);
        }

        internal ValueTask<Microsoft.AspNetCore.Html.HtmlString> Body()
        {
            return RenderBodyAsync();
        }

        internal ValueTask<Microsoft.AspNetCore.Html.HtmlString> Section(string name)
        {
            return RenderSectionAsync(name);
        }

        public RazorSlice CreateLayout()
        {
            var slice = layout();
            slice.Activation = RazorSliceFactory.GetActivation(slice.GetType());
            return slice;
        }

        public override Task ExecuteAsync()
        {
            return render(this);
        }
    }

    private sealed class LayoutProxy : IRazorSliceProxy
    {
        public static RazorSlice CreateSlice()
        {
            throw new InvalidOperationException("The supplied layout factory should be used.");
        }
    }
}
