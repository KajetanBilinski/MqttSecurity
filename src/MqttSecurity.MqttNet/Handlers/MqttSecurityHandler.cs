using System.Collections;
using System.Net;
using Microsoft.Extensions.Options;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Contracts;

namespace MqttSecurity.MqttNet.Handlers;

public sealed class MqttSecurityHandler
{
    private static readonly object ReservationKey = new();
    private readonly IMqttAuthenticationService _authentication;
    private readonly IMqttAuthorizationService _authorization;
    private readonly IMqttClientIdBindingService _clientIdBinding;
    private readonly MqttSecurityOptions _options;

    public MqttSecurityHandler(
        IMqttAuthenticationService authentication,
        IMqttAuthorizationService authorization,
        IMqttClientIdBindingService clientIdBinding,
        IOptions<MqttSecurityOptions> options)
    {
        _authentication = authentication;
        _authorization = authorization;
        _clientIdBinding = clientIdBinding;
        _options = options.Value;
    }

    public void Attach(MqttServer server)
    {
        server.ValidatingConnectionAsync += context =>
        {
            var username = context.UserName;
            var password = context.Password;

            if (string.IsNullOrEmpty(username) || password is null ||
                !_authentication.Authenticate(username, password))
            {
                context.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                return Task.CompletedTask;
            }

            if (_options.EnableClientIdBinding)
            {
                if (context.CleanSession != true ||
                    context.SessionExpiryInterval != 0)
                {
                    context.ReasonCode =
                        MqttConnectReasonCode.NotAuthorized;

                    return Task.CompletedTask;
                }
            }

            if (_options.EnableClientIdBinding)
            {
                if (!_clientIdBinding.TryReserve(
                    username, context.ClientId, out var reservationId))
                {
                    context.ReasonCode =
                        MqttConnectReasonCode.NotAuthorized;

                    return Task.CompletedTask;
                }

                try
                {
                    context.SessionItems[ReservationKey] = reservationId;
                }
                catch
                {
                    _clientIdBinding.Release(context.ClientId, reservationId);
                    throw;
                }
            }

            return Task.CompletedTask;
        };

        server.ClientConnectedAsync += async context =>
        {
            if (!_options.EnableClientIdBinding)
                return;

            if (!TryGetReservationId(context.SessionItems, out var reservationId) ||
                !_clientIdBinding.Confirm(context.ClientId, reservationId))
            {
                await server.DisconnectClientAsync(
                    context.ClientId,MqttDisconnectReasonCode.NotAuthorized);
            }
        };

        server.InterceptingPublishAsync += context =>
        {
            bool allowed = IsConnectionActive(context.ClientId, context.SessionItems) && 
            _authorization.CanPublish(
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
            bool allowed = IsConnectionActive(context.ClientId, context.SessionItems) && 
            _authorization.CanSubscribe(
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

        server.ClientDisconnectedAsync += context =>
        {
            if (TryGetReservationId(context.SessionItems, out var reservationId))
                _clientIdBinding.Release(context.ClientId, reservationId);

            return Task.CompletedTask;
        };
    }

    private bool IsConnectionActive(string clientId, IDictionary sessionItems)
    {
        if (!_options.EnableClientIdBinding)
            return true;

        return TryGetReservationId(sessionItems, out var reservationId)
            && _clientIdBinding.IsActive(clientId, reservationId);
    }

    private bool TryGetReservationId(IDictionary sessionItems, out Guid reservationId)
    {
        if (sessionItems[ReservationKey] is Guid id)
        {
            reservationId = id;
            return true;
        }

        reservationId = Guid.Empty;
        return false;
    }
}