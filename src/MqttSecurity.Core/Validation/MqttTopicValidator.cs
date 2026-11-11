namespace MqttSecurity.Core.Validation;

public static class MqttTopicValidator
{
    public static bool IsValidTopic(string? topic)
    {
        return !string.IsNullOrEmpty(topic)
            && !topic.StartsWith('$')
            && !topic.Contains('\0')
            && !topic.Contains('+')
            && !topic.Contains('#');
    }

    public static bool IsValidFilter(string? filter)
    {
        if (string.IsNullOrEmpty(filter) ||
            filter.StartsWith('$'))
            return false;

        ReadOnlySpan<char> span = filter.AsSpan();

        for (int i = 0; i < span.Length; i++)
        {
            char current = span[i];

            if (current == '\0')
                return false;

            if (current == '#')
            {
                if (i != span.Length - 1 ||
                    (i > 0 && span[i - 1] != '/'))
                    return false;
            }

            if (current == '+')
            {
                if ((i > 0 && span[i - 1] != '/') ||
                    (i < span.Length - 1 && span[i + 1] != '/'))
                    return false;
            }
        }

        return true;
    }
}