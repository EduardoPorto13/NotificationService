using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Interfaces;

public interface INotificationProvider
{
    NotificationChannel SupportedChannel { get; }
    Task<bool> SendAsync(NotificationLog message, CancellationToken cancellationToken = default);
}
