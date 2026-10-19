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

    This provides compile-time validation that you're passing the correct model type.

    Proxies construct the compiled Razor template directly with `new`, including setting its model in an object initializer. This requires a Razor compiler that emits template types accessible to application code. Property injection is initialized lazily before rendering; reflective construction is only used for replacement types supplied by Hot Reload.

    The generator-facing `SliceDefinition` and `SliceDefinition<TModel>` APIs have been removed. Rebuild applications and Razor Class Libraries with the matching source generator and runtime when upgrading. The generated proxy `Create` methods and `IRazorSliceProxy` interfaces are unchanged.

1. Add a minimal API to return the slice in your *Program.cs*:

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

### Streaming sample

Run `dotnet run --project samples/WebApp --framework net10.0` and open `/streaming`
(also linked as **Streaming** in the sample's footer). The page immediately flushes
"Counting down... 10", then flushes a plain JavaScript `<script>` block every second
to update the number through zero. After showing zero for one second, it replaces
the message with "Countdown complete!".

The sample uses `FlushAsync()` in the Razor slice and cancellation-aware server-side
delays, with no JavaScript libraries or additional requests. The endpoint opts out
of the sample's development response buffering with `.DisableResponseBuffering()`,
so updates also stream when using the default launch profiles. This helper works
on individual endpoints and route groups. It adds `DisableResponseBufferingAttribute`
metadata implementing `IDisableResponseBufferingMetadata`; the attribute can also
be applied directly to a handler method. The buffering middleware runs after routing
and checks the selected endpoint's metadata, leaving all other endpoints buffered.

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

The library is still new and features are being actively added.

### Currently supported

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
- `RazorSlice` implements `IResult` so you can return a slice instance from a minimal APIs route handler directly, or via `Results.Extensions.RazorSlice<Hello>()` and in .NET 10+ `Results.RazorSlice<Hello>()`. When using slices with models use `Results.Extensions.RazorSlice<Hello, MyModel>(model)` and in .NET 10+ `Results.RazorSlice<Hello, MyModel>(model)`.
- Full support for trimming and native AOT when used in conjunction with ASP.NET Core Minimal APIs
- Generated Razor Slice proxy types are `public sealed partial class` by default. To use record classes instead, making them `public sealed partial record`, set the `RazorSliceProxiesAsRecords` property to `true` in your project file, e.g.:
    ``` xml
    <PropertyGroup>
      <RazorSliceProxiesAsRecords>true</RazorSliceProxiesAsRecords>
    </PropertyGroup>
    ```
  
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

Import the helper's namespace (the owning project's root namespace plus the helper's directory), then call its methods through `Helpers`. The helper property name matches the `.cshtml` filename:

```cshtml
@using MyLibrary.Helpers

@Helpers.UiHelpers.StatusBadge("Completed", success: true)
@await Helpers.UiHelpers.NoticeAsync("Summary", LoadMessageAsync)
```

**Helpers require C# 14 or newer**, both in the project defining them and in projects using them. Set `<LangVersion>14.0</LangVersion>` and use a compatible SDK. .NET 8+ runtime targets remain supported; regular slices do not require C# 14.

Each helper file must inherit from `RazorSliceHelpers` (or a derived base) and declare exactly one public interface with `@implements`. Directives can be shared through `_ViewImports.cshtml`.

Helper methods return `void`, `Task`, or non-generic `ValueTask`. Call asynchronous methods with `@await`; do not use `async void`. There is no need to return `HtmlString.Empty` from a helper. Interfaces can inherit other interfaces, and methods can use overloads, generics/constraints, nullable parameters, optional/default arguments, `params`, and by-reference parameters. Do not declare `ExecuteAsync()`, which is reserved.

To share helpers with another project, reference their Razor class library or NuGet package and import the helper namespace. Consumers use the same `Helpers.UiHelpers.Method(...)` syntax and do not need the helper source. Method bodies support Hot Reload; changing public method signatures may require rebuilding/restarting.

Put helper markup inside methods in `@functions`, not in a top-level slice body. Helpers do not support models, layouts, sections, or `@inject`. Pass dependencies as method arguments. Do not use `this` or declare instance fields, properties, events, or constructors in helpers; static utilities and constants are allowed.

Helpers write directly into the calling slice's output, using its HTML encoder. A root render shares one instance of each helper implementation across its layouts, partials, and sections; separate root renders remain independent. Only use helpers during the current render: await asynchronous calls before continuing to write, do not save helper references for later, and do not write through them concurrently. Within a helper, use `Helpers` to call other helpers, `CancellationToken` for cancellable operations, and `FlushAsync()` to explicitly flush the output.

To share a helper call as a delegate without capturing the current render, use a static context-taking lambda:

```csharp
private static readonly Func<IHelperContext, string, HtmlString> RenderStatus =
    static (context, text) => context.UiHelpers.StatusBadge(text);
```

The sample app's `/helpers` page demonstrates local and class-library helpers, custom signatures/delegates, conditional attributes, generic methods, and async callbacks. `TodoHelpers.TaskStatus` calls the shared `UiHelpers.StatusBadge`, and `TodoHelpers.TaskSummaryAsync` awaits `UiHelpers.AsyncNoticeAsync`, demonstrating synchronous and asynchronous helper composition. The existing todo table also uses the composed task-status helper.

### Interested in supporting but not sure yet

- Extensions to help support using HTMX with Razor Slices 
- Getting small updates to the Razor compiler itself to get some usability and performance improvements, e.g.:
  - Don't mark the template's `ExecuteAsync` method as an `async` method unless the template contains `await` statements to save on the async state machine overhead
  - Support compiling static template elements to UTF8 string literals (`ReadOnlySpan<byte>`) instead of string literals to save on the UTF16 to UTF8 conversion during rendering
  - Support disabling the default registered `@addtaghelper` and `@model` directives which rely on MVC

### No intention to support

- Tag Helpers and View Components (they're tied to MVC and are intrinsically "heavy")
- `@model` directive (the Razor compiler does not support its use in conjunction with custom base-types via `@inherits`)
- `@attribute [Authorize]` (wrong layer of abstraction for minimal APIs, etc.)
- `@section` directive (the Razor compiler emits code that is incompatible with the rendering approach of Razor Slices)
