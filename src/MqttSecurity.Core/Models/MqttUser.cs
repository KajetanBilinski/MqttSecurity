using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace MqttSecurity.Core.Models;

public sealed class MqttUser
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }
    [JsonPropertyName("passwordHash")]
    public required string PasswordHash { get; init; }
    [JsonPropertyName("permissions")]
    public MqttPermissions Permissions { get; init; } = new MqttPermissions();
}
