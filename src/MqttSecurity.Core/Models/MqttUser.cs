using System.Collections.Immutable;

namespace MqttSecurity.Core.Models;

public sealed class MqttUser
{
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public MqttPermissions Permissions { get; init; } = new MqttPermissions();
}
