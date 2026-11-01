namespace RazorSlices.Samples.RazorClassLibrary.Slices;

public partial class FromLibrary
{
    public static async Task<string> ExampleCustomRenderAsync()
    {
        return await Create().RenderAsync();
    }
}
