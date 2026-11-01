using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using RazorSlices.Samples.WebApp.Models;
using RazorSlices.Samples.WebApp.Services;
using RazorSlices.Samples.WebApp.Slices.HtmxTodos;

namespace RazorSlices.Samples.WebApp;

internal static class HtmxTodoRoutes
{
    public static IEndpointRouteBuilder MapHtmxTodoRoutes(this IEndpointRouteBuilder endpoints)
    {
        var todos = endpoints.MapGroup("/htmx-todo");
#if !NET11_0_OR_GREATER
        todos.WithAntiforgery();
#endif

        todos.MapGet("/", GetIndex);
        todos.MapPost("/", Create);
        todos.MapPut("/{id:int}", SetComplete);
        todos.MapDelete("/{id:int}", Delete)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return endpoints;
    }

    private static IResult GetIndex(HtmxTodoStore store) => TodoIndex.Create(store.GetAll());

    private static IResult Create([FromForm] string title, HtmxTodoStore store)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > HtmxTodo.MaxTitleLength)
        {
            return Results.Text($"Enter a title between 1 and {HtmxTodo.MaxTitleLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return _Todo.Create(store.Add(title.Trim()));
    }

    private static IResult SetComplete(int id, [FromForm] bool isComplete, HtmxTodoStore store)
    {
        var todo = store.SetComplete(id, isComplete);
        return todo is null ? Results.NotFound() : _Todo.Create(todo);
    }

    private static IResult Delete(int id, HtmxTodoStore store)
    {
        // HTMX swaps an empty 200 response; a 204 would leave the row in the DOM.
        return store.Remove(id) ? Results.Ok() : Results.NotFound();
    }
}
