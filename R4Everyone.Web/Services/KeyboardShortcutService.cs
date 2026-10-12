using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

/// <summary>
/// Raises events for the app's keyboard shortcuts: Ctrl+Z to undo, and Ctrl+Y or Ctrl+Shift+Z to redo (Cmd on
/// macOS). Shortcuts are ignored while typing in a text field, which keeps its own undo.
/// </summary>
public sealed class KeyboardShortcutService(IJSRuntime js, ILogger<KeyboardShortcutService> logger) : IAsyncDisposable
{
    private DotNetObjectReference<KeyboardShortcutService>? _dotNetRef;

    public event Action? UndoRequested;
    public event Action? RedoRequested;

    public async Task RegisterAsync()
    {
        if (_dotNetRef != null)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        try
        {
            await js.InvokeVoidAsync("r4eShortcuts.register", _dotNetRef);
        }
        catch (JSException ex)
        {
            logger.LogError(ex, "Could not register keyboard shortcuts. Use the Undo and Redo buttons instead.");
        }
    }

    [JSInvokable]
    public void OnShortcut(string shortcut)
    {
        switch (shortcut)
        {
            case "undo":
                UndoRequested?.Invoke();
                break;
            case "redo":
                RedoRequested?.Invoke();
                break;
            default:
                logger.LogWarning("Ignoring unknown keyboard shortcut {Shortcut}.", shortcut);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef == null)
        {
            return;
        }

        try
        {
            await js.InvokeVoidAsync("r4eShortcuts.unregister");
        }
        catch (JSException ex)
        {
            logger.LogWarning(ex, "Could not unregister keyboard shortcuts.");
        }

        _dotNetRef.Dispose();
    }
}
