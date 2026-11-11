namespace MqttSecurity.ConfigGenerator.Models;

internal sealed record MqttUserOutput
{
    public required string Username { get; init; }

    public required string PasswordHash { get; init; }

    public required MqttPermissions Permissions { get; init; }
}
