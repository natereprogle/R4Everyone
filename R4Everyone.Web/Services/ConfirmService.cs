namespace R4Everyone.Web.Services;

public sealed record ConfirmRequest(string Title, string Message, string ConfirmLabel, bool IsDestructive = true);

/// <summary>
/// Asks the user to confirm an action. The dialog itself is rendered by ConfirmHost.
/// </summary>
public sealed class ConfirmService
{
    private TaskCompletionSource<bool>? _pending;

    public ConfirmRequest? Current { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Shows the dialog and completes with true if the user confirms. Asking again while a dialog is open cancels
    /// the earlier one.
    /// </summary>
    public Task<bool> AskAsync(ConfirmRequest request)
    {
        _pending?.TrySetResult(false);
        _pending = new TaskCompletionSource<bool>();
        Current = request;
        Changed?.Invoke();
        return _pending.Task;
    }

    public void Respond(bool confirmed)
    {
        var pending = _pending;
        _pending = null;
        Current = null;
        Changed?.Invoke();
        pending?.TrySetResult(confirmed);
    }
}
