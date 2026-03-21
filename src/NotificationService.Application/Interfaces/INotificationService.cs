using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface INotificationService
{
    Task HandleAsync(NotificationMessageDto message, CancellationToken cancellationToken = default);
}
