namespace RazorSlices.Samples.RazorClassLibrary.Slices;

public partial class FromLibrary
{
    public class Model
    {
        public required string Message { get; init; }
    }

    public static async Task<string> ExampleCustomRenderAsync(Model model)
    {
        return await Create(model).RenderAsync();
    }
}
