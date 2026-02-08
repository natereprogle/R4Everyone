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
}

public sealed class ToastService
{
    private readonly List<ToastMessage> _toasts = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _dismissTokens = new();

    public event Action? ToastsChanged;

    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    public void ShowInfo(string message) => Show(message, ToastState.Info);

    public void ShowWarning(string message) => Show(message, ToastState.Warning);

    public void ShowError(string message) => Show(message, ToastState.Error);

    public void Show(string message, ToastState state)
    {
        var toast = new ToastMessage
        {
            Message = message,
            State = state
        };

        AddToast(toast);
    }

    public void Dismiss(Guid id)
    {
        if (_dismissTokens.Remove(id, out var token))
        {
            token.Cancel();
            token.Dispose();
        }

        var removed = _toasts.RemoveAll(toast => toast.Id == id) > 0;
        if (removed)
        {
            NotifyChanged();
        }
    }

    private void AddToast(ToastMessage toast)
    {
        if (_toasts.Count >= 3)
        {
            var oldest = _toasts[0];
            Dismiss(oldest.Id);
        }

        _toasts.Add(toast);
        ScheduleDismiss(toast);
        NotifyChanged();
    }

    private void ScheduleDismiss(ToastMessage toast)
    {
        var tokenSource = new CancellationTokenSource();
        _dismissTokens[toast.Id] = tokenSource;

        _ = AutoDismissAsync(toast.Id, tokenSource.Token);
    }

    private async Task AutoDismissAsync(Guid id, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        Dismiss(id);
    }

    private void NotifyChanged()
    {
        ToastsChanged?.Invoke();
    }
}
