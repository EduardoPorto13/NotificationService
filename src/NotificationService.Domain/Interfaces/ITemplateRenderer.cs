using NotificationService.Domain.Entities;

using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Interfaces;

public interface ITemplateRenderer
{
    Task<string> RenderAsync(string templateName, NotificationChannel channel, IDictionary<string, string> data, CancellationToken cancellationToken = default);
}
