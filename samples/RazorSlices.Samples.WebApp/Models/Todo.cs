using System.Collections.Concurrent;
using System.Text;

namespace RazorSlices.Samples.WebApp.Models;

public class Todo
{
  public int Id { get; set; }

  private byte[] _titleUtf8 = [];
  public byte[] TitleUtf8
  {
    get => _titleUtf8;
    set
    {
      _titleUtf8 = value;
      _title = null!; // _title will be set in the getter of its property
    }
  }

  private string? _title;
  public string Title
  {
    get => _title ??= _titleUtf8 is not null ? Encoding.UTF8.GetString(_titleUtf8) : string.Empty;
    set
    {
      _titleUtf8 = value is not null ? Encoding.UTF8.GetBytes(value) : [];
      _title = value;
    }
  }

  public DateOnly? DueBy { get; set; }

  public bool IsComplete { get; set; }
}

internal static class Todos
{
  public readonly static ConcurrentDictionary<int, Todo> AllTodos = new ConcurrentDictionary<int, Todo>(
      [
        new KeyValuePair<int, Todo>(1, new Todo { Id = 1, TitleUtf8 = "Wash the dishes."u8.ToArray(), IsComplete = true }),
        new KeyValuePair<int, Todo>(2, new Todo { Id = 2, TitleUtf8 = "Dry the dishes."u8.ToArray(), IsComplete = true }),
        new KeyValuePair<int, Todo>(3, new Todo { Id = 3, TitleUtf8 = "Turn the dishes over."u8.ToArray(), DueBy = DateOnly.FromDateTime(DateTime.Now), IsComplete = false }),
        new KeyValuePair<int, Todo>(4, new Todo { Id = 4, TitleUtf8 = "Walk the kangaroo."u8.ToArray(), DueBy = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), IsComplete = false }),
        new KeyValuePair<int, Todo>(5, new Todo { Id = 5, TitleUtf8 = "Call Grandma."u8.ToArray(), DueBy = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), IsComplete = false })
      ]
  );
}