namespace RazorSlices;

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files.
/// </summary>
/// <returns>A <see cref="RazorSlice" /> instance.</returns>
public delegate RazorSlice SliceFactory();

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files with <c>@inject</c> properties.
/// </summary>
/// <returns>A <see cref="RazorSlice" /> instance.</returns>
public delegate RazorSlice SliceWithServicesFactory(IServiceProvider serviceProvider);

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files with strongly-typed models.
/// </summary>
/// <returns>A <see cref="RazorSlice{TModel}" /> instance.</returns>
public delegate RazorSlice<TModel> SliceFactory<TModel>(TModel model);

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files with strongly-typed models and <c>@inject</c> properties.
/// </summary>
/// <returns>A <see cref="RazorSlice{TModel}" /> instance.</returns>
public delegate RazorSlice<TModel> SliceWithServicesFactory<TModel>(TModel model, IServiceProvider serviceProvider);
