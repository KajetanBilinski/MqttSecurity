using System.Text.Json;
using System.Text.Json.Serialization;
using MqttSecurity.ConfigGenerator.Models;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Validation;

namespace MqttSecurity.ConfigGenerator.Services;

internal sealed class MqttConfigurationGenerator
{
    private readonly IPasswordHasher _passwordHasher;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow
        };

    public MqttConfigurationGenerator(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public async Task<int> GenerateAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        string input = Path.GetFullPath(inputPath);
        string output = Path.GetFullPath(outputPath);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(input, output, comparison))
        {
            throw new InvalidOperationException(
                "Input and output cannot be the same file.");
        }

        if (File.Exists(output))
        {
            throw new IOException(
                $"Output file already exists: {output}");
        }

        await using var inputStream = File.OpenRead(input);

        var setupUsers = await JsonSerializer.DeserializeAsync<
            List<MqttUserSetup?>>(
                inputStream,
                JsonOptions,
                cancellationToken);

        if (setupUsers is null)
        {
            throw new InvalidDataException(
                "Setup JSON cannot be null.");
        }

        Validate(setupUsers);

        var users = new List<MqttUserOutput>(setupUsers.Count);

        foreach (var setup in setupUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = setup!;

            users.Add(new MqttUserOutput
            {
                Username = user.Username!,
                PasswordHash = _passwordHasher.HashPassword(
                    user.Password!),
                Permissions = user.Permissions!
            });
        }

        string directory = Path.GetDirectoryName(output)!;

        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(output)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var outputStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    outputStream,
                    users,
                    JsonOptions,
                    cancellationToken);

                await outputStream.FlushAsync(cancellationToken);
            }
            File.Move(tempPath, output);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        return users.Count;
    }

    private static void Validate(
        IReadOnlyCollection<MqttUserSetup?> users)
    {
        var usernames = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var user in users)
        {
            if (user is null)
            {
                throw new InvalidDataException(
                    "User entry cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(user.Username))
            {
                throw new InvalidDataException(
                    "Username is required.");
            }

            if (!usernames.Add(user.Username))
            {
                throw new InvalidDataException(
                    $"Duplicate username: {user.Username}");
            }

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                throw new InvalidDataException(
                    $"Password is required for: {user.Username}");
            }

            if (user.Permissions is null)
            {
                throw new InvalidDataException(
                    $"Permissions are required for: {user.Username}");
            }

            var permissions = user.Permissions;

            if (permissions.Publish.IsDefault ||
                permissions.Subscribe.IsDefault)
            {
                throw new InvalidDataException(
                    $"Uninitialized permissions: {user.Username}");
            }

            ValidateFilters(
                user.Username,
                permissions.Publish);

            ValidateFilters(
                user.Username,
                permissions.Subscribe);
        }
    }

    private static void ValidateFilters(
        string username,
        IEnumerable<string> filters)
    {
        foreach (string filter in filters)
        {
            if (!MqttTopicValidator.IsValidFilter(filter))
            {
                throw new InvalidDataException(
                    $"Invalid topic filter for '{username}': '{filter}'");
            }
        }
    }
   
}
