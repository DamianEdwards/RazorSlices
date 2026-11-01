using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Html;

namespace RazorSlices;

/// <summary>Adapts an asynchronous helper to a Razor expression without an additional async state machine.</summary>
[SuppressMessage("Performance", "CA1815", Justification = "A single-consumption awaitable has no equality semantics.")]
public readonly struct HelperCompletion
{
    private readonly ValueTask _pending;

    /// <summary>Wraps a helper operation. Await this value exactly once.</summary>
    public HelperCompletion(ValueTask pending)
    {
        _pending = pending;
    }

    /// <summary>Wraps a helper operation.</summary>
    public HelperCompletion(Task pending)
    {
        _pending = new(pending);
    }

    /// <summary>Gets the awaiter used by Razor expressions.</summary>
    public Awaiter GetAwaiter()
    {
        return new(_pending.GetAwaiter());
    }

    /// <summary>Awaits a helper operation and returns an empty rendering marker.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("Performance", "CA1815", Justification = "An awaiter has no equality semantics.")]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly ValueTaskAwaiter _pending;
        internal Awaiter(ValueTaskAwaiter pending)
        {
            _pending = pending;
        }

        /// <summary>Gets whether the operation completed.</summary>
        public bool IsCompleted => _pending.IsCompleted;
        /// <summary>Consumes the operation, propagating its failure or cancellation.</summary>
        public HtmlString GetResult()
        {
            _pending.GetResult();
            return HtmlString.Empty;
        }
        /// <inheritdoc />
        public void OnCompleted(Action continuation)
        {
            _pending.OnCompleted(continuation);
        }

        /// <inheritdoc />
        public void UnsafeOnCompleted(Action continuation)
        {
            _pending.UnsafeOnCompleted(continuation);
        }
    }
}
