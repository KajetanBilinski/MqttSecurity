namespace MqttSecurity.ConfigGenerator.Models;

internal sealed record MqttUserSetup
{
    public required string Username { get; init; }

    public required string Password { get; init; }

    public MqttPermissions Permissions { get; init; } = new();
}
