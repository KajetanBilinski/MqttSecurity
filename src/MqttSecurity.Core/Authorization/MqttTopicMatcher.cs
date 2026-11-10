using MqttSecurity.Core.Contracts;

namespace MqttSecurity.Core.Authorization;

public class MqttTopicMatcher : IMqttTopicMatcher
{
    public bool CoversFilter(
        string allowedFilter,
        string requestedFilter)
    {
        if (!IsValidFilter(allowedFilter) ||
            !IsValidFilter(requestedFilter))
            return false;

        var allowed = allowedFilter.Split('/');
        var requested = requestedFilter.Split('/');

        // Wildcard nie obejmuje topiców zaczynających się od $
        if (requested[0].StartsWith('$') &&
            (allowed[0] is "+" or "#"))
            return false;

        // Równoważne zakresy dla poprawnych topiców MQTT
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
        if (!IsValidTopic(topic) || !IsValidFilter(filter))
            return false;

        var topicLevels = topic.Split('/');
        var filterLevels = filter.Split('/');

        // Wildcard na początku nie obejmuje topiców systemowych
        if (topic.StartsWith('$') &&
            (filterLevels[0] is "+" or "#"))
            return false;

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
    private static bool IsValidTopic(string? topic)
    {
        return !string.IsNullOrEmpty(topic) &&
               !topic.Contains('\0') &&
               !topic.Contains('+') &&
               !topic.Contains('#');
    }

    private static bool IsValidFilter(string? filter)
    {
        if (string.IsNullOrEmpty(filter) ||
            filter.Contains('\0') ||
            filter.StartsWith("$share/", StringComparison.Ordinal))
            return false;

        var levels = filter.Split('/');

        for (int i = 0; i < levels.Length; i++)
        {
            var level = levels[i];

            if (level.Contains('#') &&
                (level != "#" || i != levels.Length - 1))
                return false;

            if (level.Contains('+') && level != "+")
                return false;
        }

        return true;
    }

}