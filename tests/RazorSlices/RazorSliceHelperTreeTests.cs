using System.IO.Pipelines;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http;

namespace RazorSlices.Tests;

public class RazorSliceHelperTreeTests
{
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

        var grandchild = new Host(async slice =>
        {
            var helper = slice.Get(Create);
            await Task.Yield();
            helper.Text("grandchild|");
        });
        var first = new Host(async slice =>
        {
            slice.Get(Create).Text("first|");
            await slice.Partial(grandchild);
            slice.Get(Create).Text("first-after|");
        });
        var sibling = new Host(slice =>
        {
            slice.Get(Create).Text("sibling|");
            return Task.CompletedTask;
        });
        var root = new Host(async slice =>
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
        var child = new Host(async slice =>
        {
            shared = slice.Get(static () => new());
            await slice.WithWriter(nestedWriter, async () =>
            {
                await Task.Yield();
                shared.Text("<\u00e9>");
            });
            throw new InvalidOperationException("child failed");
        });
        var root = new Host(async slice =>
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
    public async Task NestedLayoutsBodiesAndSectionsShareInitializationHelperUntilRootFinishes(bool pipe, bool asynchronous)
    {
        var created = new List<Helper>();
        Helper Create()
        {
            var helper = new Helper();
            created.Add(helper);
            return helper;
        }
        var partial = new Host(async slice =>
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
        }, () => inner);
        content.Initialize = (slice, _, _) =>
        {
            ((Content)slice).Get(Create).Text("init|");
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
        var child = new Host(async slice =>
        {
            shared = slice.Get(static () => new());
            await Task.Yield();
            throw new OperationCanceledException();
        });
        var root = new Host(async slice => await slice.Partial(child));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Render(root, pipe));
        Assert.Throws<InvalidOperationException>(() => shared!.Text("after"));
    }

    [Fact]
    public async Task IndependentRootsWithSameRequestDoNotShareHelpers()
    {
        var request = new DefaultHttpContext();
        Helper? first = null;
        Helper? second = null;
        var root = new Host(slice =>
        {
            first = slice.Get(static () => new());
            return Task.CompletedTask;
        })
        { HttpContext = request };
        var other = new Host(slice =>
        {
            second = slice.Get(static () => new());
            return Task.CompletedTask;
        })
        { HttpContext = request };

        await root.RenderAsync(TextWriter.Null);
        await other.RenderAsync(TextWriter.Null);
        Assert.NotSame(first, second);
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
        }, () => layout);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await content.RenderAsync(TextWriter.Null));
        Assert.Throws<InvalidOperationException>(() => shared!.Text("after"));
    }

    private static async Task<string> Render(RazorSlice slice, bool pipe)
    {
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

    private class Host(Func<Host, Task> render) : RazorSlice
    {
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
            return render(this);
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
        public RazorSlice CreateLayout()
        {
            return layout();
        }

        protected internal override Task ExecuteSectionAsync(string sectionName)
        {
            return section(this, sectionName);
        }
    }

    private sealed class OuterLayout(Func<OuterLayout, Task> render) : RazorLayoutSlice
    {
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

        public override Task ExecuteAsync()
        {
            return render(this);
        }
    }

    private sealed class ModelLayout(Func<ModelLayout, Task> render, Func<RazorSlice> layout)
        : RazorLayoutSlice<int>, IUsesLayout<LayoutProxy>
    {
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
            return layout();
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
