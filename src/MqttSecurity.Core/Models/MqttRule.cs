using MqttSecurity.Core.Enums;

namespace MqttSecurity.Core.Models;

internal sealed class MqttRule
{
    public required string TopicFilter { get; init; }
    public required MqttAction Action { get; init; }
}
