using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Persistence.Context;

namespace NotificationService.Infrastructure.Persistence.Repositories;

public class NotificationLogRepository : INotificationLogRepository
{
    private readonly NotificationDbContext _context;

    public NotificationLogRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        await _context.NotificationLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        _context.NotificationLogs.Update(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
