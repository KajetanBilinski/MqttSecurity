using System.Collections.Immutable;

public sealed record MqttPermissions
{
    public ImmutableArray<string> Publish { get; init; } = [];

    public ImmutableArray<string> Subscribe { get; init; } = [];
}