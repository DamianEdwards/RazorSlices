using RazorSlices;
using Templates = OptimizerSample;

(Func<RazorSliceBase> Create, string Expected)[] templates =
[
    (() => Templates.Hello.Create(), "Hello 42"),
    (() => Templates.Minimal.Create(), "<p>minimal</p>"),
    (() => Templates.MinimalModel.Create("<model>"), "<em>&lt;model&gt;</em>"),
    (() => Templates.FullModel.Create("<model>"), "<strong>&lt;model&gt;</strong>"),
    (() => Templates.CustomMinimal.Create("<model>"), "<i>&lt;model&gt;</i>"),
    (() => Templates.CustomFull.Create("<model>"), "<b>&lt;model&gt;</b>")
];
foreach (var (create, expected) in templates)
{
    using var slice = create();
    var actual = (await slice.RenderAsync()).Trim();
    if (actual != expected)
    {
        throw new InvalidOperationException($"Unexpected output from {slice.GetType()}: {actual}");
    }
}
using var composed = Templates.Composed.Create();
var html = await composed.RenderAsync();
if (!html.Contains("<span>&lt;helper&gt;</span>", StringComparison.Ordinal) ||
    !html.Contains("<em>&lt;child&gt;</em>", StringComparison.Ordinal))
{
    throw new InvalidOperationException($"Helper/minimal composition failed: {html}");
}
Console.WriteLine("Optimized full, minimal, model, custom-base, and helper templates rendered successfully.");
