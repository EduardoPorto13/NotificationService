using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Interfaces;

public interface INotificationLogRepository
{
    Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task UpdateAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
