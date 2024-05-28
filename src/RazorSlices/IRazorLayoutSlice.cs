namespace RazorSlices;

internal interface IRazorLayoutSlice
{
    Func<Task>? ContentRenderer { set; }
    Func<string, Task>? SectionContentRenderer { set; }
}
