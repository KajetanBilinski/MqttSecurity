namespace MqttSecurity.Core.Models;

public readonly record struct MqttLoginProtectionDecision(
    bool IsAllowed,
    TimeSpan RetryAfter);