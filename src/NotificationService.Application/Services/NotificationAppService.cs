using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Services;

public class NotificationAppService : INotificationService
{
    private readonly IMessageProcessor _messageProcessor;
    private readonly IAppLogger _logger;

    public NotificationAppService(
        IMessageProcessor messageProcessor,
        IAppLogger logger)
    {
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    public async Task HandleAsync(NotificationMessageDto message, CancellationToken cancellationToken = default)
    {
        _logger.Info($"NotificationAppService: Recebida mensagem para processo {message.Type}...");
        
        await _messageProcessor.ProcessAsync(message, cancellationToken);
        
        _logger.Info($"NotificationAppService: Processamento de {message.Type} finalizado.");
    }
}
