namespace Community.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T eventData, CancellationToken cancellationToken = default);
}
