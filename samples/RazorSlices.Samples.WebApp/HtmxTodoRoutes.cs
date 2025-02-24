using RazorSlices.Samples.WebApp.Models;
using Slices = RazorSlices.Samples.WebApp.Slices;
using Microsoft.AspNetCore.Mvc;

public static class HtmxTodoRoutes
{
  public static void MapHtmxTodoRoutes(this IEndpointRouteBuilder app)
  {
    var htmxTodos = app.MapGroup("/htmx-todo");

    htmxTodos.MapGet("/", HandleGetIndex);
    htmxTodos.MapPost("/", HandlePostIndex);
    htmxTodos.MapPut("/{id:int}/toggle-complete", HandleToggleComplete);
    htmxTodos.MapDelete("/{id:int}", HandleDeleteTodo);
  }

  private static IResult HandleGetIndex()
  {g
    var todos = Todos.AllTodos.Values.ToList();
    return Results.Extensions.RazorSlice<Slices.HtmxTodos.TodoIndex, List<Todo>>(todos);
  }

  private static IResult HandlePostIndex([FromForm] string title)
  {
    var maxId = Todos.AllTodos.Keys.DefaultIfEmpty(0).Max();
    var newTodo = new Todo { Id = maxId + 1, Title = title };
    Todos.AllTodos.TryAdd(newTodo.Id, newTodo);

    var todos = Todos.AllTodos.Values.ToList();
    return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todos, List<Todo>>(todos);
  }

  private static IResult HandleToggleComplete(int id)
  {
    if (Todos.AllTodos.TryGetValue(id, out var todo))
    {
      todo.IsComplete = !todo.IsComplete;

      var todos = Todos.AllTodos.Values.ToList();
      return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todos, List<Todo>>(todos);
    }

    return Results.NotFound();
  }

  private static IResult HandleDeleteTodo(int id)
  {
    if (Todos.AllTodos.TryRemove(id, out var _))
    {
      var todos = Todos.AllTodos.Values.ToList();
      return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todos, List<Todo>>(todos);
    }
    return Results.NotFound();
  }

}