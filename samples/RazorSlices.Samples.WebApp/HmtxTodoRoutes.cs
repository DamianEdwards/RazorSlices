using RazorSlices.Samples.WebApp.Models;
using Slices = RazorSlices.Samples.WebApp.Slices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Antiforgery;
using System.Collections.Generic;
using System.Linq;

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
  {
    return Results.Extensions.RazorSlice<Slices.HtmxTodos.TodoIndex, List<Todo>>(Todos.AllTodos);
  }

  private static IResult HandlePostIndex([FromForm] string title)
  {
    var maxId = Todos.AllTodos.Select(x => x.Id).DefaultIfEmpty(0).Max();
    Todos.AllTodos.Add(new Todo { Id = maxId + 1, Title = title });
    return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todos, List<Todo>>(Todos.AllTodos);
  }

  private static IResult HandleToggleComplete(int id)
  {
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    if (todo is null)
    {
      return Results.NotFound();
    }

    todo.IsComplete = !todo.IsComplete;
    return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todos, List<Todo>>(Todos.AllTodos);
  }

  private static IResult HandleDeleteTodo(int id)
  {
    var todo = Todos.AllTodos.FirstOrDefault(t => t.Id == id);
    if (todo is null)
    {
      return Results.NotFound();
    }

    Todos.AllTodos.Remove(todo);
    return Results.Extensions.RazorSlice<Slices.HtmxTodos._Todos, List<Todo>>(Todos.AllTodos);
  }

}