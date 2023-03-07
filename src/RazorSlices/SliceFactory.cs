namespace RazorSlices;

/// <summary>
/// A delegate for creating instances of the types generated for .cshtml template files.
/// </summary>
/// <returns>A <see cref="RazorSlice" /> instance.</returns>
public delegate RazorSlice SliceFactory();
