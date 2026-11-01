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

[RazorCompiledItemMetadata("Identifier", "/Minimal.cshtml")]
internal sealed class MinimalSlice : RazorSliceBase
{
    [RazorInject] public IHtmlHelper<dynamic> Html { get; private set; } = default!;
    [RazorInject] public IJsonHelper Json { get; private set; } = default!;
    [RazorInject] public IViewComponentHelper Component { get; private set; } = default!;
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    [RazorInject] public IModelExpressionProvider ModelExpressionProvider { get; private set; } = default!;

    public override Task ExecuteAsync()
    {
        WriteLiteral("Minimal");
        return Task.CompletedTask;
    }
}

[RazorCompiledItemMetadata("Identifier", "/MinimalModel.cshtml")]
internal sealed class MinimalModelSlice : RazorSliceBase<string>
{
    [RazorInject] public IJsonHelper Json { get; private set; } = default!;
    public override Task ExecuteAsync()
    {
        Write(Model);
        return Task.CompletedTask;
    }
}

internal abstract class CustomMinimalBase<T> : RazorSliceBase<T>;

[RazorCompiledItemMetadata("Identifier", "/CustomMinimal.cshtml")]
internal sealed class CustomMinimalSlice : CustomMinimalBase<string>
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}

internal abstract class CustomFullBase<T> : RazorSlice<T>;

[RazorCompiledItemMetadata("Identifier", "/CustomFull.cshtml")]
internal sealed class CustomFullSlice : CustomFullBase<string>
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}

[RazorCompiledItemMetadata("Identifier", "/Helper.cshtml")]
internal sealed class HelperImplementation : RazorSliceHelpers
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}

internal abstract class CustomHelperBase : RazorSliceHelpers;

[RazorCompiledItemMetadata("Identifier", "/CustomHelper.cshtml")]
internal sealed class CustomHelperImplementation : CustomHelperBase
{
    [RazorInject] public IUrlHelper Url { get; private set; } = default!;
    public override Task ExecuteAsync() => Task.CompletedTask;
}
