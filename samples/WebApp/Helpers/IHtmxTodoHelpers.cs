using Microsoft.AspNetCore.Http;
using RazorSlices.Samples.WebApp.Models;

namespace RazorSlices.Samples.WebApp.Helpers;

public interface IHtmxTodoHelpers
{
    void CreateForm(HttpContext? httpContext, bool useAntiforgeryTokens);
    void Row(HtmxTodo todo);
}
