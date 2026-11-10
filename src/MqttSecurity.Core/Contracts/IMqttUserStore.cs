using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Contracts;

internal interface IMqttUserStore
{
    MqttUser? GetUser(string username);
    IReadOnlyList<MqttUser> GetAllUsers();
    bool Exists(string username);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
