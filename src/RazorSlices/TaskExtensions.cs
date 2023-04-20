namespace System.Threading.Tasks;

internal static class TaskExtensions
{
    public static bool HandleSynchronousCompletion(this Task task)
    {
        if (task.IsCompletedSuccessfully)
        {
            task.GetAwaiter().GetResult();
            return true;
        }
        return false;
    }

    public static bool HandleSynchronousCompletion(this ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
        {
            task.GetAwaiter().GetResult();
            return true;
        }
        return false;
    }
}
