# AGENTS.md

## Project Overview

**RazorSlices** is a lightweight Razor-based template library for ASP.NET Core without MVC, Razor Pages, or Blazor. It's optimized for high-performance, unbuffered rendering with low allocations and is compatible with trimming and native AOT.

## Repository Structure

```
src/
  RazorSlices/                    # Main library (net8.0, ASP.NET Core framework ref)
  RazorSlices.SourceGenerator/    # Roslyn incremental source generator (netstandard2.0)
samples/
  RazorSlices.Samples.WebApp/     # Main sample app (multi-TFM: net8.0, net9.0)
  RazorSlices.Samples.PagesAndSlices/  # Sample showing Razor Pages + Slices coexistence
  RazorSlices.Samples.RazorClassLibrary/ # Sample Razor Class Library
tests/
  RazorSlices.Tests/              # Unit tests for library
  RazorSlices.SourceGenerator.Tests/ # Unit tests for source generator
  RazorSlices.Samples.WebApp.Tests/  # Integration tests (WebApplicationFactory)
  RazorSlices.Samples.WebApp.PublishTests/ # Publish/AOT tests
  BenchmarksWebApps/              # Benchmark projects
```

## Key Files

- `src/RazorSlices/IRazorSliceProxy.cs` — Interfaces for generated proxy types (`IRazorSliceProxy` for no-model, `IRazorSliceProxy<TModel>` for model slices)
- `src/RazorSlices/IResultExtensions.cs` — `Results.Extensions.RazorSlice<T>()` extension methods
- `src/RazorSlices/IUsesLayout.cs` — Layout support interfaces
- `src/RazorSlices/SliceDefinition.cs` — Runtime slice factory (reflection + expression trees)
- `src/RazorSlices/RazorSlice.cs` — Base class for all slices
- `src/RazorSlices/RazorSlice.Partials.cs` — Partial rendering support
- `src/RazorSlices.SourceGenerator/RazorSliceProxyGenerator.cs` — The incremental source generator
- `src/RazorSlices.SourceGenerator/RazorDirectiveParser.cs` — Parses @inherits/@using from .cshtml
- `src/RazorSlices.SourceGenerator/ViewImportsResolver.cs` — Hierarchical _ViewImports resolution
- `src/RazorSlices.SourceGenerator/ModelTypeResolver.cs` — Resolves model types to FQN via Compilation
- `src/RazorSlices/build/RazorSlices.props` — MSBuild properties for consumers
- `src/RazorSlices/build/RazorSlices.targets` — MSBuild targets that configure AdditionalTexts for the generator
- `src/Directory.Build.props` — Version and package metadata

## Build & Test

```shell
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/RazorSlices.SourceGenerator.Tests
dotnet test tests/RazorSlices.Samples.WebApp.Tests

# Run the sample web app
dotnet run --project samples/RazorSlices.Samples.WebApp
```

## Conventions

- **Source generator targets `netstandard2.0`** — required by Roslyn. No `Span<T>`, no modern .NET APIs. Uses `LangVersion: Latest` for language features.
- **Library targets `net8.0`** with ASP.NET Core framework reference.
- **Central package management** via `Directory.Packages.props`.
- **Versioning** in `src/Directory.Build.props` (`VersionPrefix`).
- **Generated proxy classes** are `public sealed` by default. Set `RazorSliceProxiesSealed=false` to make them `public partial`.
- **`_ViewImports.cshtml`** files are inherited hierarchically — child directories inherit directives from parents up to the project root. `@inherits` from the most specific file wins; `@using` accumulates.
- All `.cshtml` files (except `_ViewImports` and `_ViewStart`) are automatically treated as Razor Slices unless `EnableDefaultRazorSlices` is set to `false`.

## Architecture Notes

### Source Generator Pipeline
The `RazorSliceProxyGenerator` is an `IIncrementalGenerator` that:
1. Collects all `.cshtml` `AdditionalText` files
2. Filters to files with `GenerateRazorSlice=true` metadata
3. Parses `@inherits` and `@using` directives from each file and its `_ViewImports.cshtml` hierarchy
4. Resolves model types to fully-qualified names using the `Compilation`
5. Generates proxy classes implementing `IRazorSliceProxy` (no model) or `IRazorSliceProxy<TModel>` (with model)

### Model Type Resolution
The generator resolves type names from Razor directives against the compilation:
- Primitives (`bool`, `int`, etc.) → mapped to `global::System.*`
- Using aliases (`Models = Namespace.Type`) → expanded and resolved
- Generic types (`Func<T1, T2>`) → parsed recursively, each argument resolved
- Array types (`Todo[]`) → element type resolved, suffix preserved
- Searched in: explicit `@using` namespaces, then implicit namespaces (`System`, `System.Collections.Generic`, etc.)

### Key Interfaces
- `IRazorSliceProxy` — no-model slices, has `static abstract RazorSlice CreateSlice()`
- `IRazorSliceProxy<TModel>` — model slices, has `static abstract RazorSlice<TModel> CreateSlice(TModel model)`
- `IUsesLayout<TLayout>` — layouts without models (constrains `TLayout : IRazorSliceProxy`)
- `IUsesLayout<TLayout, TModel>` — layouts with models (constrains `TLayout : IRazorSliceProxy<TModel>`)
