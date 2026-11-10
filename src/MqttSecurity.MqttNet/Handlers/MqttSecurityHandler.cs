using System.Net;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttSecurity.Core.Contracts;

namespace MqttSecurity.MqttNet.Handlers;

public sealed class MqttSecurityHandler
{
    private readonly IMqttAuthenticationService _authentication;
    private readonly IMqttAuthorizationService _authorization;

    public MqttSecurityHandler(
        IMqttAuthenticationService authentication,
        IMqttAuthorizationService authorization)
    {
        _authentication = authentication;
        _authorization = authorization;
    }

    public void Attach(MqttServer server)
    {
        server.ValidatingConnectionAsync += context =>
        {
            var username = context.UserName;
            var password = context.Password;

            if (username is null ||
                password is null ||
                !_authentication.Authenticate(username, password))
            {
                context.ReasonCode =
                    MqttConnectReasonCode.BadUserNameOrPassword;
            }

            return Task.CompletedTask;
        };

        server.InterceptingPublishAsync += context =>
        {
            bool allowed = _authorization.CanPublish(
                context.UserName,
                context.ApplicationMessage.Topic);

            if (!allowed)
            {
                context.ProcessPublish = false;
                context.Response.ReasonCode =
                    MqttPubAckReasonCode.NotAuthorized;
            }

            return Task.CompletedTask;
        };

        server.InterceptingSubscriptionAsync += context =>
        {
            bool allowed = _authorization.CanSubscribe(
                context.UserName,
                context.TopicFilter.Topic);

            if (!allowed)
            {
                context.ProcessSubscription = false;
                context.Response.ReasonCode =
                    MqttSubscribeReasonCode.NotAuthorized;
            }

            return Task.CompletedTask;
        };
    }
}