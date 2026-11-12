namespace MqttSecurity.Core.Configuration;

public sealed class MqttLoginProtectionOptions
{
    public bool Enabled { get; set; } = true;

    public int MaxFailuresPerAccount { get; set; } = 5;

    public int MaxFailuresPerIpAddress { get; set; } = 20;

    public TimeSpan FailureWindow { get; set; }
        = TimeSpan.FromMinutes(1);

    public TimeSpan BlockDuration { get; set; }
        = TimeSpan.FromMinutes(2);
}