using RazorSlices.Samples.WebApp.Slices.Lorem;
using System.Text;

namespace RazorSlices.Samples.WebApp.Services;

public class LoremService
{
    public string Sentences(int length)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < length; i++)
        {
            sb.Append(PageContent.Sentence);
        }
        return sb.ToString();
    }
}
