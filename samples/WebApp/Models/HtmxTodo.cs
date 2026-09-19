using Microsoft.AspNetCore.Antiforgery;

namespace RazorSlices.Samples.WebApp.Models;

public sealed record HtmxTodo(int Id, string Title, bool IsComplete = false)
{
    public const int MaxTitleLength = 200;
}

public sealed record HtmxTodoPage(IReadOnlyList<HtmxTodo> Todos, AntiforgeryTokenSet Antiforgery);
