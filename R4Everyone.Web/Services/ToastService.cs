using Microsoft.Extensions.Logging;

namespace R4Everyone.Web.Services;

public enum ToastState
{
    Info,
    Warning,
    Error
}

public sealed class ToastMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Message { get; init; } = string.Empty;
    public ToastState State { get; init; }
    public bool IsClosing { get; set; }
}

/// <summary>
/// Shows short notifications. At most <see cref="MaxVisible"/> are shown at once; each closes itself after
/// <see cref="AutoDismissDelay"/>.
/// </summary>
public sealed class ToastService(ILogger<ToastService> logger)
{
    private const int MaxVisible = 3;
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(5);

    // Matches the exit animation in app.css.
    private static readonly TimeSpan ExitAnimationDuration = TimeSpan.FromMilliseconds(250);

    private readonly List<ToastMessage> _toasts = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _dismissTokens = new();

    public event Action? ToastsChanged;

    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    public void ShowInfo(string message) => Show(message, ToastState.Info);

    public void ShowWarning(string message) => Show(message, ToastState.Warning);

    public void ShowError(string message) => Show(message, ToastState.Error);

    public void Show(string message, ToastState state)
    {
        if (_toasts.Count >= MaxVisible)
        {
            Remove(_toasts[0].Id);
        }

        var toast = new ToastMessage { Message = message, State = state };
        var dismissToken = new CancellationTokenSource();
        _dismissTokens[toast.Id] = dismissToken;
        _toasts.Add(toast);
        NotifyChanged();

        _ = DismissAfterAsync(toast.Id, AutoDismissDelay, dismissToken.Token);
    }

    /// <summary>
    /// Starts the exit animation, then removes the toast.
    /// </summary>
    public void Dismiss(Guid id)
    {
        var toast = _toasts.Find(item => item.Id == id);
        if (toast == null || toast.IsClosing)
        {
            return;
        }

        toast.IsClosing = true;
        CancelAutoDismiss(id);
        NotifyChanged();

        _ = RemoveAfterAsync(id, ExitAnimationDuration);
    }

    private async Task DismissAfterAsync(Guid id, TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            Dismiss(id);
        }
        catch (OperationCanceledException)
        {
            // Dismissed or removed before the timer elapsed.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-dismiss toast {ToastId}.", id);
        }
    }

    private async Task RemoveAfterAsync(Guid id, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            Remove(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove toast {ToastId}.", id);
        }
    }

    private void Remove(Guid id)
    {
        CancelAutoDismiss(id);
        if (_toasts.RemoveAll(toast => toast.Id == id) > 0)
        {
            NotifyChanged();
        }
    }

    private void CancelAutoDismiss(Guid id)
    {
        if (_dismissTokens.Remove(id, out var token))
        {
            token.Cancel();
            token.Dispose();
        }
    }

    private void NotifyChanged() => ToastsChanged?.Invoke();
}
