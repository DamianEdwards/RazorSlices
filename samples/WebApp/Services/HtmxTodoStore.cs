using RazorSlices.Samples.WebApp.Models;

namespace RazorSlices.Samples.WebApp.Services;

internal sealed class HtmxTodoStore
{
    private readonly object _lock = new();
    private readonly Dictionary<int, HtmxTodo> _todos = new()
    {
        [1] = new(1, "Try adding a todo.", true),
        [2] = new(2, "Mark a todo complete."),
        [3] = new(3, "Delete a todo.")
    };
    private int _nextId = 3;

    public IReadOnlyList<HtmxTodo> GetAll()
    {
        lock (_lock)
        {
            return _todos.Values.OrderBy(todo => todo.Id).ToArray();
        }
    }

    public HtmxTodo Add(string title)
    {
        lock (_lock)
        {
            var todo = new HtmxTodo(checked(++_nextId), title);
            _todos.Add(todo.Id, todo);
            return todo;
        }
    }

    public HtmxTodo? SetComplete(int id, bool isComplete)
    {
        lock (_lock)
        {
            if (!_todos.TryGetValue(id, out var todo))
            {
                return null;
            }

            var updated = todo with { IsComplete = isComplete };
            _todos[id] = updated;
            return updated;
        }
    }

    public bool Remove(int id)
    {
        lock (_lock)
        {
            return _todos.Remove(id);
        }
    }
}
