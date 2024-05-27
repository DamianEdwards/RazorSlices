namespace RazorSlices;

internal interface IRazorLayoutSlice
{
    Func<Task>? ContentRenderer { set; }
}
