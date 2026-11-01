namespace RazorSlices.Samples.RazorClassLibrary.Helpers;

public interface IUiHelpers
{
    void StatusBadge(string text, bool success = false, string? title = null);
    ValueTask AsyncNoticeAsync(string heading, Func<CancellationToken, ValueTask<string>> loadMessage);
}
