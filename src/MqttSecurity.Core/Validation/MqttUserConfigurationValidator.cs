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

            if (user.Permissions.Publish.Length == 0 && user.Permissions.Subscribe.Length == 0)
                throw new InvalidDataException(
                    $"Missing permissions: {user.Username}");

            foreach (var topic in user.Permissions.Publish)
            {
                if (topic is null)
                    throw new InvalidDataException(
                        $"Null topic: {user.Username}");

                if (!MqttTopicValidator.IsValidFilter(topic))
                    throw new InvalidDataException(
                        $"Invalid topic: {topic}");
            }
            foreach (var filter in user.Permissions.Subscribe)
            {
                if (filter is null)
                    throw new InvalidDataException(
                        $"Null topic: {user.Username}");

                if (!MqttTopicValidator.IsValidFilter(filter))
                    throw new InvalidDataException(
                        $"Invalid topic filter: {filter}");
            }
        }
    }
}