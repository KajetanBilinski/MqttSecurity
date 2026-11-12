using System.Collections;
using System.Net;
using Microsoft.Extensions.Options;
using MQTTnet.Adapter;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Contracts;

namespace MqttSecurity.MqttNet.Handlers;

public sealed class MqttSecurityHandler
{
    private static readonly object ReservationKey = new();
    private static readonly object ConnectionKey = new();
    private readonly IMqttAuthenticationService _authentication;
    private readonly IMqttAuthorizationService _authorization;
    private readonly IMqttClientIdBindingService _clientIdBinding;
    private readonly IMqttLoginProtectionService _loginProtection;
    private readonly MqttSecurityOptions _options;
    private readonly Func<ValidatingConnectionEventArgs, Task>? _additionalValidation;

    public MqttSecurityHandler(
        IMqttAuthenticationService authentication,
        IMqttAuthorizationService authorization,
        IMqttClientIdBindingService clientIdBinding,
        IMqttLoginProtectionService loginProtection,
        IOptions<MqttSecurityOptions> options,
        Func<ValidatingConnectionEventArgs, Task>? additionalValidation = null)
    {
        _authentication = authentication;
        _authorization = authorization;
        _clientIdBinding = clientIdBinding;
        _loginProtection = loginProtection;
        _options = options.Value;
        _additionalValidation = additionalValidation;
    }

    public void Attach(MqttServer server)
    {
        server.ValidatingConnectionAsync += async context =>
        {
            if (context.ReasonCode != MqttConnectReasonCode.Success)
                return;

            var username = context.UserName;
            var password = context.Password;
            var remoteEndpoint = context.ChannelAdapter.RemoteEndPoint as IPEndPoint;

            if (remoteEndpoint == null)
            {
                context.ReasonCode = MqttConnectReasonCode.NotAuthorized;
                return;
            }

            var ipAddress = remoteEndpoint.Address;

            if (username == null)
            {
                username = string.Empty;
            }

            if (!_loginProtection.Check(username, ipAddress).IsAllowed)
            {
                context.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;

                return;
            }



            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) ||
                !_authentication.Authenticate(username, password))
            {
                _loginProtection.RegisterFailure(username, ipAddress);
                context.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                return;
            }

            _loginProtection.RegisterSuccess(username);

            if (_options.EnableClientIdBinding)
            {
                if (context.CleanSession != true ||
                    context.SessionExpiryInterval != 0)
                {
                    context.ReasonCode = MqttConnectReasonCode.NotAuthorized;

                    return;
                }
            }

            if (_additionalValidation != null)
                await _additionalValidation(context);

            if (context.ReasonCode != MqttConnectReasonCode.Success)
                return;

            context.CancellationToken.ThrowIfCancellationRequested();

            if (_options.EnableClientIdBinding)
            {
                if (!_clientIdBinding.TryReserve(
                    username, context.ClientId, out var reservationId))
                {
                    context.ReasonCode = MqttConnectReasonCode.NotAuthorized;

                    return;
                }

                try
                {
                    context.SessionItems[ReservationKey] = reservationId;
                    context.SessionItems[ConnectionKey] = context.ChannelAdapter;
                }
                catch
                {
                    _clientIdBinding.Release(context.ClientId, reservationId);
                    throw;
                }
            }

        };

        server.ClientConnectedAsync += async context =>
        {
            if (!_options.EnableClientIdBinding)
                return;

            if (!TryGetReservationId(context.SessionItems, out var reservationId) ||
                !_clientIdBinding.Confirm(context.ClientId, reservationId))
            {
                var connection = context.SessionItems[ConnectionKey]
                    as IMqttChannelAdapter;
                if (connection != null)
                    await connection.DisconnectAsync(CancellationToken.None);
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
