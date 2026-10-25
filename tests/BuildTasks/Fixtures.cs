using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.Hosting;

namespace RazorSlices.BuildTasks.Tests;

[RazorCompiledItemMetadata("Identifier", "/Unused.cshtml")]
internal sealed class UnusedSlice : RazorSlice
{
    [RazorInject] public IHtmlHelper<dynamic> Html { get; private set; } = default!;
    [RazorInject] public IJsonHelper Json { get; private set; } = default!;
    [RazorInject] public IViewComponentHelper Component { get; private set; } = default!;
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    [RazorInject] public IModelExpressionProvider ModelExpressionProvider { get; private set; } = default!;
    [RazorInject] public IServiceProvider CustomService { get; private set; } = default!;

    public override Task ExecuteAsync()
    {
        WriteLiteral("Hello 42");
        return Task.CompletedTask;
    }
}

[RazorCompiledItemMetadata("Identifier", "/Referenced.cshtml")]
internal sealed class ReferencedSlice : RazorSlice
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;

    public override Task ExecuteAsync()
    {
        Write(Url);
        return Task.CompletedTask;
    }
}

[RazorCompiledItemMetadata("Identifier", "/Generic.cshtml")]
internal sealed class GenericSlice : RazorSlice<string>
{
    [RazorInject] public IJsonHelper Json { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}

[RazorCompiledItemMetadata("Identifier", "/Mvc.cshtml")]
internal sealed class NonSlice
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
}

[RazorCompiledItemMetadata("Identifier", "/Public.cshtml")]
public sealed class PublicSlice : RazorSlice
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}

[RazorCompiledItemMetadata("Identifier", "/NotAutoProperty.cshtml")]
internal sealed class NotAutoPropertySlice : RazorSlice
{
    [RazorInject] public IUrlHelper Url => throw new NotSupportedException();
    public override Task ExecuteAsync() => Task.CompletedTask;
}

internal sealed class AuthoredSlice : RazorSlice
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}
