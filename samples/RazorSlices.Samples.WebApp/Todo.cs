namespace RazorSlices.Samples.WebApp;

public class Todo
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public DateOnly? DueBy { get; set; }

    public bool IsComplete { get; set; }
}

internal static class Todos
{
    public readonly static Todo[] AllTodos = new Todo[]
        {
            new() { Id = 1, Title = "Wash the dishes.", IsComplete = true },
            new() { Id = 2, Title = "Dry the dishes.", IsComplete = true },
            new() { Id = 3, Title = "Turn the dishes over.", DueBy = DateOnly.FromDateTime(DateTime.Now), IsComplete = false },
            new() { Id = 4, Title = "Walk the kangaroo.", DueBy = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), IsComplete = false },
            new() { Id = 5, Title = "Call Grandma.", DueBy = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), IsComplete = false },
        };
}