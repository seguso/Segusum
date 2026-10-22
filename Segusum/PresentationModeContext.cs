using System.Threading;

namespace Seg;

public enum PresentationMode
{
    Graphics = 0,
    Text = 1,
    TextWithRoomBackgroundPreview = 2
}

/// <summary>
/// Carries the current client's presentation preference through one asynchronous
/// request. A cached World can be shared by requests, so presentation mode must
/// never be stored on the World instance.
/// </summary>
public static class PresentationModeContext
{
    public const string RequestHeaderName = "X-Segusum-Text-Mode";

    private static readonly AsyncLocal<PresentationMode?> currentMode = new();

    /// <summary>
    /// The current request mode. Missing and invalid values default to graphics.
    /// </summary>
    public static PresentationMode Mode
    {
        get => currentMode.Value ?? PresentationMode.Graphics;
        set => currentMode.Value = value;
    }

    /// <summary>
    /// Compatibility view for existing gameplay and rendering code. Modes 1 and 2
    /// both use the existing text-mode behavior.
    /// </summary>
    public static bool IsTextMode
    {
        get => Mode != PresentationMode.Graphics;
        set => Mode = value ? PresentationMode.Text : PresentationMode.Graphics;
    }

    /// <summary>
    /// Parse the transport value centrally. Missing and invalid values select graphics.
    /// </summary>
    public static PresentationMode ResolveRequestHeader(string? value) => value switch
    {
        "1" => PresentationMode.Text,
        "2" => PresentationMode.TextWithRoomBackgroundPreview,
        _ => PresentationMode.Graphics
    };

    /// <summary>
    /// Resolves direct engine calls that still provide the derived boolean. Requests
    /// with a mode established by ApiBase always use that tri-state value.
    /// </summary>
    public static PresentationMode ForRoomResponse(bool isTextMode) =>
        currentMode.Value ?? (isTextMode ? PresentationMode.Text : PresentationMode.Graphics);

    /// <summary>
    /// Establishes a temporary mode for direct engine calls and nested work.
    /// </summary>
    public static IDisposable Push(bool isTextMode)
        => Push(isTextMode ? PresentationMode.Text : PresentationMode.Graphics);

    public static IDisposable Push(PresentationMode mode)
    {
        var previous = currentMode.Value;
        currentMode.Value = mode;
        return new RestoreScope(previous);
    }

    private sealed class RestoreScope(PresentationMode? previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            currentMode.Value = previous;
            disposed = true;
        }
    }
}
