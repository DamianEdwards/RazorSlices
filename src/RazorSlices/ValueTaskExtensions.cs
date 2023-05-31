// Taken from https://github.com/dotnet/aspnetcore/blob/4eae56d8f7315cbd49fcbd760341940e3d087aa5/src/Shared/ValueTaskExtensions/ValueTaskExtensions.cs

using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace RazorSlices;

internal static class ValueTaskExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task GetAsTask(this in ValueTask<FlushResult> valueTask)
    {
        // Try to avoid the allocation from AsTask
        if (valueTask.IsCompletedSuccessfully)
        {
            // Signal consumption to the IValueTaskSource
            var _ =valueTask.GetAwaiter().GetResult();
            return Task.CompletedTask;
        }
        else
        {
            return valueTask.AsTask();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask GetAsValueTask(this in ValueTask<FlushResult> valueTask)
    {
        // Try to avoid the allocation from AsTask
        if (valueTask.IsCompletedSuccessfully)
        {
            // Signal consumption to the IValueTaskSource
            var _ = valueTask.GetAwaiter().GetResult();
            return default;
        }
        else
        {
            return new ValueTask(valueTask.AsTask());
        }
    }
}
