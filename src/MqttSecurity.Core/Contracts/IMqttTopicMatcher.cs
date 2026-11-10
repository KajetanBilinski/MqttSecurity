namespace MqttSecurity.Core.Contracts;

public interface IMqttTopicMatcher
{
    bool Matches(string topic, string topicFilter);
    bool CoversFilter(string topic, string topicFilter);
}
