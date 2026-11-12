using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MqttSecurity.Core.Authentication;
using MqttSecurity.Core.Authorization;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Protection;
using MqttSecurity.Core.Storage;

namespace MqttSecurity.Core.Extensions;

public static class MqttSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddMqttSecurity(
    this IServiceCollection services,
    Action<MqttSecurityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<MqttSecurityOptions>()
            .Configure(configure)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UsersFilePath),
                "UsersFilePath cannot be empty.");

        services.AddSingleton<IMqttUserStore>(sp =>
        {
            var options = sp
                .GetRequiredService<IOptions<MqttSecurityOptions>>()
                .Value;

            return new JsonMqttUserStore(options.UsersFilePath);
        });

        services.AddSingleton<IPasswordHasher, MqttPasswordHasher>();

        services.AddSingleton<IMqttAuthenticationService, MqttAuthenticationService>();

        services.AddSingleton<IMqttAuthorizationService, MqttAuthorizationService>();

        services.AddSingleton<IMqttTopicMatcher, MqttTopicMatcher>();

        services.AddSingleton<IMqttClientIdBindingService,MqttClientIdBindingService>();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IMqttLoginProtectionService,MqttLoginProtectionService>();

        return services;
    }
}