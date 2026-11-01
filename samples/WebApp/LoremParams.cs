namespace RazorSlices.Samples.WebApp;
public struct LoremParams
{
    public int ParagraphCount;
    public int ParagraphSentenceCount;

    public LoremParams(int? paragraphCount, int? paragraphSentenceCount)
    {
        ParagraphCount = paragraphCount ?? 3;
        ParagraphSentenceCount = paragraphSentenceCount ?? 5;
    }
}
