namespace RazorSlices.Samples.WebApp.Helpers;

public interface ITodoHelpers
{
    void ActionButton(string operation, int id, string text, bool disabled = false);
    void Statistic<T>(string label, T value) where T : ISpanFormattable;
}
