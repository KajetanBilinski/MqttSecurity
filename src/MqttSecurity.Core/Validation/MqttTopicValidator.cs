namespace MqttSecurity.Core.Validation;

internal static class MqttTopicValidator
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
            filter.StartsWith('$') ||
            filter.Contains('\0'))
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