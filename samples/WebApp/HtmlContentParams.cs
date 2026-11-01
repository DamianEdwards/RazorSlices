namespace RazorSlices.Samples.WebApp;

public struct HtmlContentParams
{
    public bool Encode;

    public HtmlContentParams(bool? encode)
    {
        Encode = encode ?? false;
    }
}
