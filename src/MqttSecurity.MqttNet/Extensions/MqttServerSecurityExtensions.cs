using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Server;
using MqttSecurity.Core.Contracts;
using MqttSecurity.MqttNet.Handlers;

namespace MqttSecurity.MqttNet.Extensions;

public static class MqttServerSecurityExtensions
{
    public static async Task UseMqttSecurityAsync(
        this MqttServer server,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(services);

        var store = services.GetRequiredService<IMqttUserStore>();

        await store.ReloadAsync(cancellationToken);

        var authentication = services
            .GetRequiredService<IMqttAuthenticationService>();

        var authorization = services
            .GetRequiredService<IMqttAuthorizationService>();

        var handler = new MqttSecurityHandler(
            authentication,
            authorization);

        handler.Attach(server);
    }
}