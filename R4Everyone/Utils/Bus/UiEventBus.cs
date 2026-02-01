using System.Collections.Concurrent;

namespace R4Everyone.Utils.Bus;

public sealed class UiEventBus : IUiEventBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    ~UiEventBus()
    {
        lock (_handlers)
        {
            _handlers.Clear();
        }
    }

    public void Publish<T>(T message)
    {
        if (!_handlers.TryGetValue(typeof(T), out var delegates)) return;

        Delegate[] snapshot;
        lock (delegates)
        {
            snapshot = [.. delegates];
        }

        foreach (var d in snapshot)
        {
            try
            {
                ((Action<T>)d).Invoke(message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in event handler: {ex}");
            }
        }
    }

    public void Subscribe<T>(Action<T> handler)
    {
        var delegates = _handlers.GetOrAdd(typeof(T), _ => []);
        lock (delegates)
        {
            delegates.Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) return;

        lock (list)
        {
            list.Remove(handler);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}