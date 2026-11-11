using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Enums;

namespace MqttSecurity.Core.Authorization;

public class MqttAuthorizationService : IMqttAuthorizationService
{
    private readonly IMqttUserStore _userStore;
    private readonly IMqttTopicMatcher _topicMatcher;
    public MqttAuthorizationService(IMqttUserStore userStore, IMqttTopicMatcher mqttTopicMatcher)
    {
        _userStore = userStore;
        _topicMatcher = mqttTopicMatcher;
    }
    public bool CanPublish(string username, string topic)
    {
        if(string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(topic))
            return false;

        var user = _userStore.GetUser(username);

        if (user is null) return false;

        return user.Permissions.Publish.Any(filter =>
            _topicMatcher.Matches(topic, filter));
    }

    public bool CanSubscribe(string username, string topicFilter)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(topicFilter))
            return false;

        var user = _userStore.GetUser(username);

        if (user is null) return false;

        return user.Permissions.Subscribe.Any(filter =>
             _topicMatcher.CoversFilter(filter, topicFilter));
    }

}
