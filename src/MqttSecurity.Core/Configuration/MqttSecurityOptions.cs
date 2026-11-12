namespace MqttSecurity.Core.Configuration;

public sealed class MqttSecurityOptions
{
    public string UsersFilePath { get; set; } = string.Empty;
    public bool EnableClientIdBinding { get; set; } = true;
    public TimeSpan PendingLifetime { get; set; } = TimeSpan.FromMinutes(1);
    public MqttLoginProtectionOptions LoginProtection { get; set; } = new();
}