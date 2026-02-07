using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

public class ViewportService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<ViewportService>? _dotNetRef;
    private bool _initialized;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsDesktop => Width >= 992 && Height >= 530;

    public event Action? ViewportChanged;

    public ViewportService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _dotNetRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("viewport.register", _dotNetRef);
    }

    [JSInvokable]
    public void OnResize(int width, int height)
    {
        Width = width;
        Height = height;
        ViewportChanged?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        return ValueTask.CompletedTask;
    }
}
