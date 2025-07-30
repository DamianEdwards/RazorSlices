using RazorSlices.Samples.WebApp.Models;
using Slices = RazorSlices.Samples.WebApp.Slices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;

public static class HtmxTodoRoutes
{
  public static void MapHtmxTodoRoutes(this IEndpointRouteBuilder app)
  {
    var htmxTodos = app.MapGroup("/htmx-todo");

    htmxTodos.MapGet("/", HandleGetIndex);
    htmxTodos.MapGet("/create", HandleGetCreateForm);
    htmxTodos.MapPost("/", HandlePostIndex);
    htmxTodos.MapPut("/{id:int}/toggle-complete", HandleToggleComplete);
    htmxTodos.MapDelete("/{id:int}", HandleDeleteTodo);
  }

  private static IResult HandleGetIndex()
  {
    var todos = Todos.AllTodos.Values.ToList();
    return Results.Extensions.RazorSlice<Slices.HtmxTodos.TodoIndex, List<Todo>>(todos);
  }
  private static IResult HandleGetCreateForm(HttpContext context, IAntiforgery antiforgery)
  {
    var token = antiforgery.GetAndStoreTokens(context);
    return Results.Extensions.RazorSlice<Slices.HtmxTodos._TodoCreateForm, AntiforgeryTokenSet>(token);
  }

  private static IResult HandlePostIndex([FromForm] string title)
  {
    var maxId = Todos.AllTodos.Keys.DefaultIfEmpty(0).Max();
    var newTodo = new Todo { Id = maxId + 1, Title = title };
    Todos.AllTodos.TryAdd(newTodo.Id, newTodo);

    return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todo, Todo>(newTodo);
  }

  private static IResult HandleToggleComplete(int id)
  {
    if (Todos.AllTodos.TryGetValue(id, out var todo))
    {
      todo.IsComplete = !todo.IsComplete;

      return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todo, Todo>(todo);
    }

    return Results.NotFound();
  }

  private static IResult HandleDeleteTodo(int id)
  {
    if (Todos.AllTodos.TryRemove(id, out var _))
    {
      return Results.Ok();
    }
    return Results.NotFound();
  }

}