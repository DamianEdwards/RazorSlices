using RazorSlices.Samples.WebApp.Slices;

namespace RazorSlices.Samples.WebApp.Services;

public class LoremService
{
    public string Sentences(int length)
    {
        string sentences = "";
        for (int i = 0; i < length; i++)
        {
            sentences += PageContent.Sentence;
        }
        return sentences;
    }
}
