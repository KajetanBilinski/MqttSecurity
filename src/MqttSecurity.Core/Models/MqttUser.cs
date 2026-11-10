using System.Collections.Immutable;

namespace MqttSecurity.Core.Models;

internal sealed class MqttUser
{
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required bool Enabled { get; init; }
    public ImmutableArray<MqttRule> Rules { get; init; } = [];
}
