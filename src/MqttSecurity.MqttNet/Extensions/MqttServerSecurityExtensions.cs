using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MQTTnet.Server;
using MqttSecurity.Core.Configuration;
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

        var authentication = services.GetRequiredService<IMqttAuthenticationService>();

        var authorization = services.GetRequiredService<IMqttAuthorizationService>();

        var clientIdBinding = services.GetRequiredService<IMqttClientIdBindingService>();
        var configuration = services.GetRequiredService<IOptions<MqttSecurityOptions>>();

        var handler = new MqttSecurityHandler(
            authentication,
            authorization,
            clientIdBinding,
            configuration);

        handler.Attach(server);
    }
}