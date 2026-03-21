namespace NotificationService.Domain.Settings;

public class LogConfig
{
    public string PathLog { get; set; } = @"C:\Logs\NotificationService";
    public string FileName { get; set; } = "NotificationServiceLog";
    public bool RunToService { get; set; } = false;
    public string EPSName { get; set; } = "NotifBase";
}

public class LogSettings
{
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB
}
