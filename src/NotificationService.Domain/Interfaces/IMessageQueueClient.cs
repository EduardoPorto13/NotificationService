namespace NotificationService.Domain.Interfaces;

public interface IMessageQueueClient
{
    Task PublishAsync<T>(T message, string routingKey, CancellationToken cancellationToken = default);
}
