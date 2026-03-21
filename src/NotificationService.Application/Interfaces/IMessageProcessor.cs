using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface IMessageProcessor
{
    Task ProcessAsync(NotificationMessageDto message, CancellationToken cancellationToken = default);
}
