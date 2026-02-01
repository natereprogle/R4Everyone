namespace R4Everyone.Utils.Bus;

public interface IUiEventBus
{
    void Publish<T>(T message);
    void Subscribe<T>(Action<T> handler);
    void Unsubscribe<T>(Action<T> handler);
}