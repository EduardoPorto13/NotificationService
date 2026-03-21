namespace NotificationService.Domain.Settings;

public class RabbitMqSettings
{
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string RetryQueueName { get; set; } = string.Empty;
    public string DeadLetterQueueName { get; set; } = string.Empty;
}
