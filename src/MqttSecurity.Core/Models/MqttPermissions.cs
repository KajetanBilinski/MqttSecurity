using System.Collections.Immutable;
using System.Text.Json.Serialization;

public sealed record MqttPermissions
{
    [JsonPropertyName("publish")]
    public ImmutableArray<string> Publish { get; init; } = [];
    [JsonPropertyName("subscribe")]
    public ImmutableArray<string> Subscribe { get; init; } = [];
}