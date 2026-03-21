using NotificationService.Domain.Settings;
using NotificationService.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace NotificationService.Infrastructure.Messaging.RabbitMq;

public class RabbitMqQueueClient : IMessageQueueClient
{
    private readonly RabbitMqSettings _settings;
    private readonly IAppLogger _logger;

    public RabbitMqQueueClient(IOptions<RabbitMqSettings> settings, IAppLogger logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task PublishAsync<T>(T message, string routingKey, CancellationToken cancellationToken = default)
    {
        _logger.Info($"[RabbitMqQueueClient] Simulando a publicação da mensagem no exchange para a RouteKey: {routingKey}");
        // Implementação real usando rabbitmq-dotnet-client IConnection e IModel
        return Task.CompletedTask;
    }
}
