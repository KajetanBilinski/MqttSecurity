using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Validation;

public static class MqttUserConfigurationValidator
{
    public static void Validate(IEnumerable<MqttUser> users)
    {
        var usernames = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var user in users)
        {
            if (user is null)
                throw new InvalidDataException(
                    "User cannot be null.");

            if (string.IsNullOrWhiteSpace(user.Username))
                throw new InvalidDataException(
                    "Username cannot be empty.");

            if (!usernames.Add(user.Username))
                throw new InvalidDataException(
                    $"Duplicate username: {user.Username}");

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                throw new InvalidDataException(
                    $"Missing password hash: {user.Username}");

            if (user.Rules.Length == 0)
                throw new InvalidDataException(
                    $"Missing rules: {user.Username}");

            foreach (var rule in user.Rules)
            {
                if (rule is null)
                    throw new InvalidDataException(
                        $"Null rule: {user.Username}");

                if (!MqttTopicValidator.IsValidFilter(rule.TopicFilter))
                    throw new InvalidDataException(
                        $"Invalid topic filter: {rule.TopicFilter}");
            }
        }
    }
}