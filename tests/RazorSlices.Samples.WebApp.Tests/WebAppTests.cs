using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RazorSlices.Samples.WebApp.Tests;

public class WebAppTests
{
  [Theory]
  [MemberData(nameof(EndpointDetails))]
  public async Task WafHosted_EndpointsRenderOK(string path, string shouldContain, string expectedMediaType)
  {
    var waf = new WebApplicationFactory<Program>();
    using var httpClient = waf.CreateClient();

    var response = await httpClient.GetAsync(path);

    response.EnsureSuccessStatusCode();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(expectedMediaType, response.Content.Headers.ContentType?.MediaType);
    Assert.Contains(shouldContain, await response.Content.ReadAsStringAsync());
  }

  public static object[][] EndpointDetails => [
      ["/", "Todos", MediaTypeNames.Text.Html],
        ["/1", "Wash the dishes", MediaTypeNames.Text.Html],
        ["/encoding", "{&#x27;antiForgery&#x27;", MediaTypeNames.Text.Html],
        ["/unicode", "🐻", MediaTypeNames.Text.Html],
        ["/templated", "This is from a partial with a templated model", MediaTypeNames.Text.Html],
        ["/library", "This slice was loaded from a referenced Razor Class Library!", MediaTypeNames.Text.Html],
        ["/render-to-string", "htmlString", MediaTypeNames.Application.Json],
        ["/render-to-stringbuilder", "htmlString", MediaTypeNames.Application.Json],
        ["/lorem", "Lorem Ipsum (Static)", MediaTypeNames.Text.Html],
        ["/lorem-static", "Lorem Ipsum (Static)", MediaTypeNames.Text.Html],
        ["/lorem-dynamic", "Lorem Ipsum (Dynamic: 3 paragraphs)", MediaTypeNames.Text.Html],
        ["/lorem-dynamic?paraCount=12&paraLength=6", "Lorem Ipsum (Dynamic: 12 paragraphs)", MediaTypeNames.Text.Html],
        ["/lorem-formattable", "Lorem Ipsum (Formattable: 3 paragraphs)", MediaTypeNames.Text.Html],
        ["/lorem-htmlcontent", "Lorem Ipsum (IHtmlContent)", MediaTypeNames.Text.Html],
        ["/lorem-htmlcontent?encode=true", "&lt;p&gt;", MediaTypeNames.Text.Html],
        ["/lorem-injectableproperties", "Lorem Ipsum (Dependency-injected properties)", MediaTypeNames.Text.Html],
        ["/lorem-stream", "Lorem Ipsum (Static)", MediaTypeNames.Text.Html],
        ["/htmx-todo", "Todos using HTMX", MediaTypeNames.Text.Html]

  ];
}
