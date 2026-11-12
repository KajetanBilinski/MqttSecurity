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
        CancellationToken cancellationToken = default,
        Func<ValidatingConnectionEventArgs, Task>? additionalValidation = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(services);

        var store = services.GetRequiredService<IMqttUserStore>();

        await store.ReloadAsync(cancellationToken);

        var authentication = services.GetRequiredService<IMqttAuthenticationService>();

        var authorization = services.GetRequiredService<IMqttAuthorizationService>();

        var clientIdBinding = services.GetRequiredService<IMqttClientIdBindingService>();

        var loginProtection = services.GetRequiredService<IMqttLoginProtectionService>();

        var configuration = services.GetRequiredService<IOptions<MqttSecurityOptions>>();

        var handler = new MqttSecurityHandler(
            authentication,
            authorization,
            clientIdBinding,
            loginProtection,
            configuration,
            additionalValidation);

        handler.Attach(server);
    }
}
