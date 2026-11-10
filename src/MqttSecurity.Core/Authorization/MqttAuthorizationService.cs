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
    public bool CanPublish(string username, string topicFilter)
    {
        if(string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(topicFilter))
            return false;

        var user = _userStore.GetUser(username);

        if (user is null) return false;

        return user.Rules.Any(r => r.Action.HasFlag(MqttAction.Publish) && _topicMatcher.Matches(topicFilter, r.TopicFilter));
    }

    public bool CanSubscribe(string username, string topicFilter)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(topicFilter))
            return false;

        var user = _userStore.GetUser(username);

        if (user is null) return false;

        return user.Rules.Any(r => r.Action.HasFlag(MqttAction.Subscribe) && _topicMatcher.Matches(topicFilter, r.TopicFilter));
    }
}
