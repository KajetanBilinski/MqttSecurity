using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Enums;
using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Storage;

internal class JsonMqttUserStore : IMqttUserStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<MqttAction>() }
    };

    private Dictionary<string, MqttUser> _users = new();
    private readonly string _filePath;

    public JsonMqttUserStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }
    public bool Exists(string? username)
    {
        return !string.IsNullOrWhiteSpace(username)
            && _users.ContainsKey(username);
    }

    public MqttUser? GetUser(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        return _users.GetValueOrDefault(username);
    }

    public IReadOnlyList<MqttUser> GetAllUsers()
    {
        return _users.Values.ToList();
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(
            _filePath,
            cancellationToken);

        var users = JsonSerializer.Deserialize<List<MqttUser>>(
            json, Options);

        if (users is null)
            throw new InvalidDataException("Invalid users configuration.");

        var dictionary = users.ToDictionary(
            user => user.Username,
            StringComparer.Ordinal);

        _users = dictionary;
    }
}
