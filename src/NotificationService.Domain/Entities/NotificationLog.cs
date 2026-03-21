using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class NotificationLog
{
    public NotificationChannel Channel { get; set; }
    public Guid Id { get; set; }
    public string? CorrelationId { get; set; }
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Status { get; set; }
    public int Attempts { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Payload { get; set; }
    public string? AttachmentsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
