using System.Collections.Immutable;
using System.Text.Json;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Models;
using MqttSecurity.Core.Validation;

namespace MqttSecurity.Core.Storage;

public class JsonMqttUserStore : IMqttUserStore
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private Dictionary<string, MqttUser> _users = new();
    private readonly string _filePath;

    public JsonMqttUserStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }
    public bool Exists(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        var users = Volatile.Read(ref _users);

        return users.ContainsKey(username);
    }

    public MqttUser? GetUser(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        var users = Volatile.Read(ref _users);

        return users.GetValueOrDefault(username);
    }

    public IReadOnlyList<MqttUser> GetAllUsers()
    {
        var users = Volatile.Read(ref _users);
        return users.Values.ToList();
    }

    public async Task ReloadAsync(
        CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken);

        try
        {
            var json = await File.ReadAllTextAsync(
                _filePath,
                cancellationToken);

            var users = JsonSerializer.Deserialize<List<MqttUser>>(
                json);

            if (users is null)
                throw new InvalidDataException(
                    "Invalid users configuration.");

            MqttUserConfigurationValidator.Validate(users);

            var dictionary = users.ToDictionary(u => u.Username, u => u);

            Volatile.Write(ref _users, dictionary);
        }
        finally
        {
            _reloadLock.Release();
        }
    }
}
