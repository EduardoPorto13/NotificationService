using NotificationService.Domain.Enums;
using System.Collections.Generic;

namespace NotificationService.Application.DTOs;

public class NotificationMessageDto
{
    public NotificationChannel Channel { get; set; } = NotificationChannel.Email;
    public string To { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public IDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    public NotificationMetadataDto? Metadata { get; set; }
    public List<AttachmentDto>? Attachments { get; set; }
}

public class NotificationMetadataDto
{
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
