using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Validation;

namespace MqttSecurity.Core.Authorization;

public class MqttTopicMatcher : IMqttTopicMatcher
{
    public bool CoversFilter(string allowedFilter,string requestedFilter)
    {
        if (!MqttTopicValidator.IsValidFilter(allowedFilter) ||
            !MqttTopicValidator.IsValidFilter(requestedFilter))
            return false;

        if (allowedFilter == "+/#" && requestedFilter == "#")
            return true;

        ReadOnlySpan<char> allowed = allowedFilter.AsSpan();
        ReadOnlySpan<char> requested = requestedFilter.AsSpan();

        while (true)
        {
            int allowedIndex = allowed.IndexOf('/');
            int requestedIndex = requested.IndexOf('/');

            var allowedLevel = allowedIndex < 0
                ? allowed
                : allowed[..allowedIndex];

            var requestedLevel = requestedIndex < 0
                ? requested
                : requested[..requestedIndex];

            if (allowedLevel.SequenceEqual("#"))
                return true;

            if (requestedLevel.SequenceEqual("#"))
                return false;

            if (!allowedLevel.SequenceEqual("+") &&
                (requestedLevel.SequenceEqual("+") ||
                 !allowedLevel.SequenceEqual(requestedLevel)))
                return false;

            if (requestedIndex < 0)
            {
                return allowedIndex < 0 ||
                       allowed[(allowedIndex + 1)..]
                           .SequenceEqual("#");
            }

            if (allowedIndex < 0)
                return false;

            allowed = allowed[(allowedIndex + 1)..];
            requested = requested[(requestedIndex + 1)..];
        }
    }

    public bool Matches(string topic, string filter)
    {
        if (!MqttTopicValidator.IsValidTopic(topic) ||
            !MqttTopicValidator.IsValidFilter(filter))
            return false;

        ReadOnlySpan<char> topicSpan = topic.AsSpan();
        ReadOnlySpan<char> filterSpan = filter.AsSpan();

        while (true)
        {
            int topicIndex = topicSpan.IndexOf('/');
            int filterIndex = filterSpan.IndexOf('/');

            var topicLevel = topicIndex < 0
                ? topicSpan
                : topicSpan[..topicIndex];

            var filterLevel = filterIndex < 0
                ? filterSpan
                : filterSpan[..filterIndex];

            if (filterLevel.SequenceEqual("#"))
                return true;

            if (!filterLevel.SequenceEqual("+") &&
                !filterLevel.SequenceEqual(topicLevel))
                return false;

            if (topicIndex < 0)
            {
                return filterIndex < 0 ||
                       filterSpan[(filterIndex + 1)..]
                           .SequenceEqual("#");
            }

            if (filterIndex < 0)
                return false;

            topicSpan = topicSpan[(topicIndex + 1)..];
            filterSpan = filterSpan[(filterIndex + 1)..];
        }
    }

}