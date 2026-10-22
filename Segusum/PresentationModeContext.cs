using System.Threading;

namespace Seg;

/// <summary>
/// Carries the current client's presentation preference through one asynchronous
/// request. A cached World can be shared by requests, so presentation mode must
/// never be stored on the World instance.
/// </summary>
public static class PresentationModeContext
{
    public const string RequestHeaderName = "X-Segusum-Text-Mode";

    private static readonly AsyncLocal<bool?> currentTextMode = new();

    /// <summary>
    /// The request mode. Requests without an explicit value default to graphics.
    /// </summary>
    public static bool IsTextMode
    {
        get => currentTextMode.Value ?? false;
        set => currentTextMode.Value = value;
    }

    /// <summary>
    /// Parse the transport value centrally. Only the explicit wire value "1"
    /// selects text mode; missing, invalid, and "0" values select graphics.
    /// </summary>
    public static bool ResolveRequestHeader(string? value) => value == "1";

    /// <summary>
    /// Establishes a temporary mode for direct engine calls and nested work.
    /// </summary>
    public static IDisposable Push(bool isTextMode)
    {
        var previous = currentTextMode.Value;
        currentTextMode.Value = isTextMode;
        return new RestoreScope(previous);
    }

    private sealed class RestoreScope(bool? previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            currentTextMode.Value = previous;
            disposed = true;
        }
    }
}
