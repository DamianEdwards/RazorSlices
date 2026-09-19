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

        todos.MapGet("/", GetIndex);
        todos.MapPost("/", Create);
        todos.MapPut("/{id:int}", SetComplete);
        todos.MapDelete("/{id:int}", Delete);

        return endpoints;
    }

    private static IResult GetIndex(HttpContext context, IAntiforgery antiforgery, HtmxTodoStore store)
    {
        // Issue the cookie before Razor Slices starts writing the response.
        var tokens = antiforgery.GetAndStoreTokens(context);
        return TodoIndex.Create(new HtmxTodoPage(store.GetAll(), tokens));
    }

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

    private static async Task<IResult> Delete(int id, HttpContext context, IAntiforgery antiforgery, HtmxTodoStore store)
    {
        // The antiforgery middleware validates POST, PUT and PATCH, but not DELETE.
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Text("Invalid antiforgery token. Reload the page and try again.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // HTMX swaps an empty 200 response; a 204 would leave the row in the DOM.
        return store.Remove(id) ? Results.Ok() : Results.NotFound();
    }
}
