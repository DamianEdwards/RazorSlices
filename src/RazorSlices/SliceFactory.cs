namespace RazorSlices;

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files.
/// </summary>
/// <returns>A <see cref="RazorSlice" /> instance.</returns>
public delegate RazorSlice SliceFactory();

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files with strongly-typed models.
/// </summary>
/// <returns>A <see cref="RazorSlice{TModel}" /> instance.</returns>
public delegate RazorSlice<TModel> SliceFactory<TModel>(TModel model);
