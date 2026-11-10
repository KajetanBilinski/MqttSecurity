using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Validation;
using static System.Net.WebRequestMethods;

namespace MqttSecurity.Core.Authorization;

public class MqttTopicMatcher : IMqttTopicMatcher
{
    public bool CoversFilter(
        string allowedFilter,
        string requestedFilter)
    {
        if (!MqttTopicValidator.IsValidFilter(allowedFilter) ||
            !MqttTopicValidator.IsValidFilter(requestedFilter))
            return false;

        var allowed = allowedFilter.Split('/');
        var requested = requestedFilter.Split('/');

        if (allowedFilter == "+/#" && requestedFilter == "#")
            return true;

        for (int i = 0; i < requested.Length; i++)
        {
            if (i >= allowed.Length)
                return false;

            var a = allowed[i];
            var r = requested[i];

            if (a == "#")
                return true;

            if (r == "#")
                return false;

            if (a != "+" &&
                !string.Equals(a, r, StringComparison.Ordinal))
                return false;
        }

        return allowed.Length == requested.Length ||
               (allowed.Length == requested.Length + 1 &&
                allowed[^1] == "#");
    }

    public bool Matches(string topic, string filter)
    {
        if (!MqttTopicValidator.IsValidTopic(topic) ||
            !MqttTopicValidator.IsValidFilter(filter))
            return false;

        var topicLevels = topic.Split('/');
        var filterLevels = filter.Split('/');

        for (int i = 0; i < topicLevels.Length; i++)
        {
            if (i >= filterLevels.Length)
                return false;

            var current = filterLevels[i];

            if (current == "#")
                return true;

            if (current == "+")
                continue;

            if (!string.Equals(
                    current,
                    topicLevels[i],
                    StringComparison.Ordinal))
                return false;
        }

        return filterLevels.Length == topicLevels.Length ||
               (filterLevels.Length == topicLevels.Length + 1 &&
                filterLevels[^1] == "#");
    }

}