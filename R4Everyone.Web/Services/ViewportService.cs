using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

/// <summary>
/// Tracks the browser window size so the app can tell whether it is running on a desktop-sized screen.
/// </summary>
public sealed class ViewportService(IJSRuntime jsRuntime, ILogger<ViewportService> logger) : IAsyncDisposable
{
    private const int MinDesktopWidth = 992;
    private const int MinDesktopHeight = 530;

    private DotNetObjectReference<ViewportService>? _dotNetRef;

    public int? Width { get; private set; }
    public int? Height { get; private set; }

    /// <summary>
    /// True until the first measurement arrives, so the editor is not hidden while the size is unknown.
    /// </summary>
    public bool IsDesktop => Width is null || Height is null || (Width >= MinDesktopWidth && Height >= MinDesktopHeight);

    public event Action? ViewportChanged;

    public async Task InitializeAsync()
    {
        if (_dotNetRef != null)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        try
        {
            await jsRuntime.InvokeVoidAsync("r4eViewport.register", _dotNetRef);
        }
        catch (JSException ex)
        {
            logger.LogError(ex, "Could not watch the window size. Assuming a desktop screen.");
        }
    }

    [JSInvokable]
    public void OnResize(int width, int height)
    {
        Width = width;
        Height = height;
        ViewportChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef == null)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("r4eViewport.unregister");
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not stop watching the window size.");
        }

        _dotNetRef.Dispose();
    }
}
