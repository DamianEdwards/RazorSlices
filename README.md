# ![Razor Slices](assets/razorslices-logo.svg)

[![CI (main)](https://github.com/DamianEdwards/RazorSlices/actions/workflows/ci.yml/badge.svg)](https://github.com/DamianEdwards/RazorSlices/actions/workflows/ci.yml)
[![Nuget](https://img.shields.io/nuget/v/RazorSlices)](https://www.nuget.org/packages/RazorSlices/)

*Lightweight* Razor-based templates for ASP.NET Core without MVC, Razor Pages, or Blazor, optimized for high-performance, unbuffered rendering with low allocations. Compatible with trimming and native AOT. Great for returning dynamically rendered HTML from Minimal APIs, middleware, etc. Supports .NET 8+

- [Getting Started](#getting-started)
- [Installation](#installation)
- [Features](#features)

## Getting Started

1. [Install the NuGet package](#installation) into your ASP.NET Core project (.NET 8+):

    ``` shell
    > dotnet add package RazorSlices
    ```

1. Create a directory in your project called *Slices* and add a *_ViewImports.cshtml* file to it with the following content:

    ``` cshtml
    @inherits RazorSlice

    @using System.Globalization;
    @using Microsoft.AspNetCore.Razor;
    @using Microsoft.AspNetCore.Http.HttpResults;
    @using RazorSlices;
    
    @tagHelperPrefix __disable_tagHelpers__:
    @removeTagHelper *, Microsoft.AspNetCore.Mvc.Razor
    ```

1. In the same directory, add a *Hello.cshtml* file with the following content:

    ``` cshtml
    @inherits RazorSlice<DateTime>
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="utf-8">
        <title>Hello from Razor Slices!</title>
    </head>
    <body>
        <p>
            Hello from Razor Slices! The time is @Model
        </p>
    </body>
    </html>
    ```

    Each *.cshtml* file will have a proxy type generated for it by the Razor Slices source generator that you can use as the generic argument to the various APIs in Razor Slices for rendering slices. The source generator detects the model type from the `@inherits` directive and generates a **strongly-typed** `Create` method:
    - Slices **with** a model get `Create(ModelType model)` (e.g., `MyApp.Slices.Hello.Create(DateTime.Now)`)
    - Slices **without** a model get `Create()` (e.g., `MyApp.Slices.Home.Create()`)

    This provides compile-time validation that you're passing the correct model type. Proxies construct the compiled Razor template directly with `new` and initialize its model if applicable. Property injection via `@inject` is initialized lazily before rendering.

1. Add a minimal API to return the slice in your *Program.cs*. Razor Slices implement `IResult` so can be returned directly from minimal APIs:

    ``` csharp
    app.MapGet("/hello", () => MyApp.Slices.Hello.Create(DateTime.Now));
    ```

1. **Optional:** By default, all *.cshtml* files in your project are treated as Razor Slices (excluding *_ViewImports.cshtml* and *_ViewStart.cshtml_*). You can change this by setting the `EnableDefaultRazorSlices` property to `false` and then including the desired `RazorSlice` items in your project file, e.g.:

    ``` xml
    <PropertyGroup>
      <EnableDefaultRazorSlices>false</EnableDefaultRazorSlices>
    </PropertyGroup>
    <ItemGroup>
      <!-- Only treat .cshtml files in Slices directory as Razor Slices -->
      <RazorSlice Include="Slices\**\*.cshtml" Exclude="Slices\**\_Layout.cshtml;Slices\**\_ViewImports.cshtml" />
    </ItemGroup>
    ```

    This will configure the Razor Slices source generator to only generate proxy types for *.cshtml* files in the *Slices* directory in your project.

## Route-handler TempData spike

The WebApp sample contains an experimental, sample-local bindable `TempData` parameter and `.WithTempData()` endpoint filter, not a shipped library API. Visit `/tempdata` to try a cookie-backed flash message:

```csharp
builder.Services.AddRazorSliceTempData();

app.MapPost("/save", (TempData tempData) =>
{
    tempData["message"] = "Changes saved.";
    return TypedResults.Redirect("/");
}).WithTempData();

app.MapGet("/", RazorSlice (TempData tempData) =>
    Slices.Home.Create((string?)tempData["message"]))
    .WithTempData();
```

`TempData` uses Minimal APIs parameter binding without loading its contents. The filter enables access while invoking the handler, including asynchronous handlers, then closes access and saves before result execution. It returns the result unchanged. Apply `.WithTempData()` to individual endpoints or a route group; applying it at both levels does not save twice. Unused TempData does not resolve the factory, load data, or save.

Use the indexer, `TryGetValue`, `Peek`, and `Keep` as with MVC TempData. Redirects do not implicitly retain already-read values. There is no public `Load` or `Save`: the filter owns persistence. Binding without `.WithTempData()` throws. Access before the filter activates, after the handler returns, or once the response starts also throws, including through captured dictionary references, collection views, and enumerators. Pass retrieved values into slice models, not live TempData. Do not execute results or render into the response inside these handlers; accessed TempData cannot be saved after response headers start. Other endpoint filters inside the TempData filter's invocation are part of the same access window.

The spike reuses MVC's dictionary, factory, and cookie provider without registering MVC. Its reflection-free serializer intentionally supports only strings and null; register a different `TempDataSerializer` for other types or MVC-compatible payloads. Existing provider/factory/serializer registrations are preserved, including those supplied by MVC. A session provider can be registered instead, with `AddSession`, an `IDistributedCache`, and `UseSession` before endpoints. Call `Session.LoadAsync` before using session TempData to avoid synchronous cache I/O. Cookie options can be configured through `AddRazorSliceTempData(options => ...)`; normal Data Protection key management, cookie consent, cookie size limits, and session concurrency constraints still apply.

Handler exceptions close access and skip saving; this is not a transaction (the session provider removes stored TempData when loading it). These guards cover the spike's `TempData` API, not deliberate access to MVC's raw factory or providers. No middleware, result wrapper, page lifecycle, or slice rendering changes are introduced.

## Installation

### NuGet Releases

[![Nuget](https://img.shields.io/nuget/v/RazorSlices)](https://www.nuget.org/packages/RazorSlices/)

This package is currently available from [nuget.org](https://www.nuget.org/packages/RazorSlices/):

``` console
> dotnet add package RazorSlices
```

### CI Builds

If you wish to use builds from this repo's `main` branch you can install them from [this repo's package feed](https://github.com/DamianEdwards/RazorSlices/pkgs/nuget/RazorSlices).

1. [Create a personal access token](https://github.com/settings/tokens/new) for your GitHub account with the `read:packages` scope with your desired expiration length:

    [<img width="583" alt="image" src="https://user-images.githubusercontent.com/249088/160220117-7e79822e-a18a-445c-89ff-b3d9ca84892f.png">](https://github.com/settings/tokens/new)

1. At the command line, navigate to your user profile directory and run the following command to add the package feed to your NuGet configuration, replacing the `<GITHUB_USER_NAME>` and `<PERSONAL_ACCESS_TOKEN>` placeholders with the relevant values:

    ``` shell
    ~> dotnet nuget add source -n GitHub -u <GITHUB_USER_NAME> -p <PERSONAL_ACCESS_TOKEN> https://nuget.pkg.github.com/DamianEdwards/index.json
    ```

1. You should now be able to add a reference to the package specifying a version from the [repository packages feed](https://github.com/DamianEdwards/RazorSlices/pkgs/nuget/RazorSlices)

1. See [these instructions](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry) for further details about working with GitHub package feeds.

## Features

- ASP.NET Core 8.0 and above
- Strongly-typed models (via `@inherits RazorSlice<MyModel>`) with **compile-time model validation** — the source generator detects the model type from the `@inherits` directive (including `_ViewImports.cshtml` hierarchy) and generates strongly-typed `Create(MyModel model)` methods on the proxy class
- Razor constructs:
  - [Implicit expressions](https://learn.microsoft.com/aspnet/core/mvc/views/razor#implicit-razor-expressions), e.g. `@someVariable`
  - [Explicit expressions](https://learn.microsoft.com/aspnet/core/mvc/views/razor#implicit-razor-expressions), e.g. `@(someBool ? thisThing : thatThing)`
  - [Control structures](https://learn.microsoft.com/aspnet/core/mvc/views/razor#control-structures), e.g. `@if()`, `@switch()`, etc.
  - [Looping](https://learn.microsoft.com/aspnet/core/mvc/views/razor#looping-for-foreach-while-and-do-while), e.g. `@for`, `@foreach`, `@while`, `@do`
  - [Code blocks](https://learn.microsoft.com/aspnet/core/mvc/views/razor#razor-code-blocks), e.g. `@{ var someThing = someOtherThing; }`
  - [Conditional attribute rendering](https://learn.microsoft.com/aspnet/core/mvc/views/razor#conditional-attribute-rendering)
  - [Functions](https://learn.microsoft.com/aspnet/core/mvc/views/razor#functions):

    ```cshtml
    @functions {
        private readonly string _someString = "A very important string";
        private int DoAThing() => 123;
    }
    ```

  - [Templated Razor methods](https://learn.microsoft.com/aspnet/core/mvc/views/razor#code-try-48), e.g.

    ```cshtml
    @inherits RazorSlice<Todo>

    <h1>@Title(Model)</h1>

    @functions {
        private IHtmlContent Title(Todo todo)
        {
            <text>Todo @todo.Id: @todo.Title</text>
            return HtmlString.Empty;
        }
    }
    ```

  - [Templated Razor delegates](https://learn.microsoft.com/aspnet/core/mvc/views/razor#templated-razor-delegates), e.g.

    ```cshtml
    @inherits RazorSlice<Todo>

    @{
        Func<object?, Microsoft.AspNetCore.Mvc.Razor.HelperResult> tmpl = @<div>
            This is a templated Razor delegate. The following value was passed in: @item
        </div>;
    }

    @tmpl(DateTime.Now)
    ```

    **NOTE: Async templated Razor delegates are *NOT* supported and will throw an exception at runtime**

- DI-activated properties via `@inject`
- Rendering slices from slices (aka partials) via `@(await RenderPartialAsync<MyPartial>())`
- [Shareable templated helpers](#shareable-templated-helpers), with `void`/asynchronous implementations and strongly typed `@Helpers.MyHelperSlice.HelperMethod(...)` calls
- Using slices as layouts for other slices, including layouts with strongly-typed models:
  - For the layout slice, inherit from `RazorLayoutSlice` or `RazorLayoutSlice<TModel>` and use `@await RenderBodyAsync()` in the layout to render the body

      ```cshtml
      @inherits RazorLayoutSlice<LayoutModel>

      <!DOCTYPE html>
      <html lang="en">
      <head>
          <title>@Model.Title</title>
          @await RenderSectionAsync("head")
      </head>
      <body>
        @await RenderBodyAsync()

        <footer>
            @await RenderSectionAsync("footer")
        </footer>
      </body>
      </html>
      ```

  - For the slice using the layout, implement `IUsesLayout<TLayout>` or `IUsesLayout<TLayout, TModel>` to declare which layout to use. If using a layout with a model, ensure you implement the `LayoutModel` property in your `@functions` block, e.g

      ```cshtml
      @inherits RazorSlice<SomeModel>
      @implements IUsesLayout<LayoutSlice, LayoutModel>

      <div>
          @* Content here *@
      </div>

      @functions {
          public LayoutModel LayoutModel => new() { Title = "My Layout" };
      }
      ```

  - Layouts can render sections via `@await RenderSectionAsync("SectionName")` and slices can render content into sections by overriding `ExecuteSectionAsync`, e.g.:

      ```cshtml
      protected override Task ExecuteSectionAsync(string name)
      {
          if (name == "lorem-header")
          {
              <p class="text-info">This page renders a custom <code>IHtmlContent</code> type that contains lorem ipsum content.</p>
          }

          return Task.CompletedTask;
      }
      ```

    **NOTE: The `@section` directive is not supported as it's incompatible with the rendering approach of Razor Slices**

- Asynchronous rendering, i.e. the template can contain `await` statements, e.g. `@await WriteTheThing()`
- Writing UTF8 `byte[]` values directly to the output
- Rendering directly to `PipeWriter`, `Stream`, `TextWriter`, `StringBuilder`, and `string` outputs, including optimizations for not boxing struct values, zero-allocation rendering of primitives like numbers, etc. (rather than just calling `ToString()` on everything)
- `RazorSliceBase` implements `IResult`, so both minimal and normal slices can be returned directly from a minimal API route handler. Normal slices also support `Results.Extensions.RazorSlice<Hello>()` and in .NET 10+ `Results.RazorSlice<Hello>()`. When using normal slices with models use `Results.Extensions.RazorSlice<Hello, MyModel>(model)` and in .NET 10+ `Results.RazorSlice<Hello, MyModel>(model)`.
- Full support for trimming and native AOT when used in conjunction with ASP.NET Core Minimal APIs
- Generated Razor Slice proxy types are `public sealed partial class` by default. To use record classes instead, making them `public sealed partial record`, set the `RazorSliceProxiesAsRecords` property to `true` in your project file, e.g.:
    ``` xml
    <PropertyGroup>
      <RazorSliceProxiesAsRecords>true</RazorSliceProxiesAsRecords>
    </PropertyGroup>
    ```
  
### Minimal slices

`RazorSlice` is the normal base class, including support for helpers, layouts, partials, and property injection.
For templates that do not need those features and for which performance is a top concern, inherit `RazorSliceBase` or `RazorSliceBase<TModel>`.
Both tiers implement `IResult`:

```cshtml
@using RazorSlices
@inherits RazorSliceBase<string>
<p class="message @Model">@Model</p>
```

Minimal slices retain HTML encoding, conditional HTML attributes, formatting, cancellation, flushing, asynchronous
execution, and direct rendering to `PipeWriter`, `TextWriter`, `Stream`, `StringBuilder`, or a string. Their generated
proxies have the same `Create()` / `Create(model)` syntax, implementing `IRazorSliceBaseProxy` /
`IRazorSliceBaseProxy<TModel>` instead of the full-slice proxy contracts. `RazorSlice<TModel>` still inherits
`RazorSlice`, while `RazorSliceBase<TModel>` inherits the minimal base.

```csharp
var html = await MyApp.Minimal.Greeting.Create("Hello").RenderAsync();
app.MapGet("/minimal", RazorSliceBase () => MyApp.Minimal.Greeting.Create("Hello"));
```

Minimal slices are HTTP results themselves: no adapter allocation is needed. Set `StatusCode`, `ContentType`,
and `HtmlEncoder` on the slice to customize the response. HTTP execution resolves an encoder from request services
when `HtmlEncoder` is unset; set it to `HtmlEncoder.Default` to avoid that lookup and request-scope cost.
Only normal slices retain `HttpContext` for injection. A normal slice can render a minimal child with
`@await RenderPartialAsync(MyApp.Minimal.Greeting.Create("Hello"))`.
On .NET 10+, `Results.RazorSlice(slice)` and `TypedResults.RazorSlice(slice)` also accept minimal instances,
setting the status code and default encoder without allocating a wrapper.

An explicit lambda return type, as above, lets the ASP.NET Core request-delegate generator identify the result
without needing to inspect the proxy emitted by the Razor Slices generator in the same compilation.

Minimal slices do not support `@inject`, helpers, layouts, sections, partial calls of their own, or templated Razor
delegates requiring a writer stack. The generator reports `RSG0004` for effective injections and layouts, including
inherited declarations; keep minimal templates outside import hierarchies that inject services. Choose `RazorSlice`
when those features are needed. The protected virtual rendering pipeline permits custom bases to add lifecycle
behavior without adding application state to every minimal slice.

See `/minimal` and `/minimal-partial` in the sample app.

### Shareable templated helpers

Use helpers to share reusable pieces of Razor markup across slices and projects. Define their methods in a public C# interface and implement them in a `.cshtml` file.

Declare the helper methods:

```csharp
namespace MyLibrary.Helpers;

public interface IUiHelpers
{
    void StatusBadge(string text, bool success = false);
    ValueTask NoticeAsync(string heading, Func<CancellationToken, ValueTask<string>> loadMessage);
}
```

Implement it in `Helpers/UiHelpers.cshtml`:

```cshtml
@using RazorSlices
@using MyLibrary.Helpers
@inherits RazorSliceHelpers
@implements IUiHelpers

@functions {
    public void StatusBadge(string text, bool success = false)
    {
        <span class="badge @(success ? "text-bg-success" : "text-bg-secondary")">@text</span>
    }

    public async ValueTask NoticeAsync(string heading, Func<CancellationToken, ValueTask<string>> loadMessage)
    {
        var message = await loadMessage(CancellationToken);
        <aside><strong>@heading</strong><p>@message</p></aside>
    }
}
```

The normal `RazorSlice` base provides `Helpers`; no service registration or injection is required.
Import the helper's namespace (the owning project's root namespace plus the helper's directory).
The helper property name matches the `.cshtml` filename:

```cshtml
@using MyLibrary.Helpers
@inherits RazorSlice

@Helpers.UiHelpers.StatusBadge("Completed", success: true)
@await Helpers.UiHelpers.NoticeAsync("Summary", LoadMessageAsync)
```

**Helpers require C# 14 or newer**, both in the project defining them and in projects using them. Set `<LangVersion>14.0</LangVersion>` and use a compatible SDK. .NET 8+ runtime targets remain supported; regular slices do not require C# 14.

Each helper file must inherit from `RazorSliceHelpers` (or a derived base) and declare exactly one public interface with `@implements`. Directives can be shared through `_ViewImports.cshtml`.

Put helper markup inside methods in `@functions`, not in a top-level slice body. Helpers do not support models, layouts, sections, or `@inject`. Pass dependencies as method arguments. Do not use `this` or declare instance fields, properties, events, or constructors in helpers; static utilities and constants are allowed.

Helpers write directly into the active slice's output, using its HTML encoder. One instance of each helper
implementation is created lazily per root render and shared throughout its nested/sibling partials, layouts,
sections, and helper composition. Captured facades follow the active slice, including minimal children.
Independent root renders have independent helper instances, even within the same HTTP request.

Helper implementations inherit their own `Helpers` property for composition and must not declare or inherit `@inject`.
Keep them outside import hierarchies that inject services, for example `Helpers/` next to `Slices/`.
Injecting at the project root also affects helper files in child folders.

Helper methods must return `void`, `Task`, or non-generic `ValueTask`. Call asynchronous methods with `@await`; do not use `async void`. There is no need to return `HtmlString.Empty` from a helper. Interfaces can inherit other interfaces, and methods can use overloads, generics/constraints, nullable parameters, optional/default arguments, `params`, and by-reference parameters. Do not declare `ExecuteAsync()`, which is reserved.

Await asynchronous helpers before continuing to write. Only sequential, nested use of a render graph is supported;
concurrent use or other misuse has undefined behavior and is not explicitly detected. Do not retain helpers beyond
their root render or invoke them outside an active render. Within a helper, use its built-in `Helpers` property to
call other helpers, `CancellationToken` for cancellable operations, and `FlushAsync()` to explicitly flush the output.

To share helpers with another project, reference their Razor class library or NuGet package and import the helper namespace. Consumers use the same `Helpers.UiHelpers.Method(...)` syntax and do not need the helper source. Method bodies support Hot Reload; changing public method signatures may require rebuilding/restarting.

The sample app's `/helpers` page demonstrates local and class-library helpers, custom signatures/delegates, conditional attributes, generic methods, and async callbacks. `TodoHelpers.TaskStatus` calls the shared `UiHelpers.StatusBadge`, and `TodoHelpers.TaskSummaryAsync` awaits `UiHelpers.AsyncNoticeAsync`, demonstrating synchronous and asynchronous helper composition. The existing todo table also uses the composed task-status helper.

### Optimized builds

When `Optimize` is `true` (normally in Release builds), Razor Slices removes unused MVC default
injected properties (`Html`, `Json`, `Component`, `Url`, and `ModelExpressionProvider`) from internal,
SDK-generated template types inheriting `RazorSliceBase`, `RazorSlice`, or `RazorSliceHelpers`, including
their generic and custom-derived forms, after compilation. Their accessors and backing fields are also
removed. This reduces per-instance storage without replacing Razor compilation or adding a runtime
dependency. MVC views and Razor Pages are not modified.

To disable this optimization, set:

```xml
<PropertyGroup>
  <RazorSlicesRemoveMvcDefaultInjectedProperties>false</RazorSlicesRemoveMvcDefaultInjectedProperties>
</PropertyGroup>
```

The optimization only runs when `Optimize` is `true`, even when explicitly enabled. Changing the
option triggers recompilation, so a clean build is not required. Each project compiling slices,
including Razor class libraries, applies the optimization to its own assembly before publishing,
trimming, ReadyToRun, or Native AOT compilation.

Properties referenced by compiled code, properties on public template types or in assemblies with
friend assemblies, and unrecognized property implementations are retained. Explicit `@inject`
declarations are preserved conservatively: a default property name explicitly injected anywhere
in the project's Razor files or imports is retained throughout that project. Other injected
properties are unchanged.

Generated source and editor IntelliSense still show the original properties. Hot Reload of rewritten
assemblies is not supported; disable this optimization if using optimized builds with Hot Reload (uncommon).
Code accessing the removed defaults solely through reflection must also disable this optimization.

### Interested in supporting but not sure yet

- Extensions to help support using HTMX with Razor Slices
- Getting small updates to the Razor compiler itself to get some usability and performance improvements, e.g.:
  - Don't mark the template's `ExecuteAsync` method as an `async` method unless the template contains `await` statements to save on the async state machine overhead
  - Support disabling the default registered `@addtaghelper` and `@model` directives which rely on MVC

### No intention to support

- Tag Helpers and View Components (they're tied to MVC and are intrinsically "heavy")
- `@model` directive (the Razor compiler does not support its use in conjunction with custom base-types via `@inherits`)
- `@attribute [Authorize]` (wrong layer of abstraction for minimal APIs, etc.)
- `@section` directive (the Razor compiler emits code that is incompatible with the rendering approach of Razor Slices)
