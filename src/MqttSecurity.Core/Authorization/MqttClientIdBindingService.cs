
using System.Diagnostics;
using Microsoft.Extensions.Options;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Authorization;

public sealed class MqttClientIdBindingService : IMqttClientIdBindingService
{
    private readonly Lock _lock = new();

    private readonly Dictionary<string, Reservation> _reservations = new();

    private readonly IMqttUserStore _userStore;
    private readonly MqttSecurityOptions _options;

    public MqttClientIdBindingService(
        IMqttUserStore userStore,
        IOptions<MqttSecurityOptions> options)
    {
        _userStore = userStore;
        _options = options.Value;
        if (_options.PendingLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "PendingLifetime must be positive.");
    }

    public bool TryReserve(string username, string clientId, out Guid reservationId)
    {
        reservationId = Guid.Empty;

        if (!_options.EnableClientIdBinding)
            return true;

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(clientId))
            return false;

        lock (_lock)
        {
            MarkExpiredPending();

            var user = _userStore.GetUser(username);

            if (user is null) return false;

            if (_reservations.ContainsKey(clientId)) return false;

            if (user.ClientIds.Count == 0)
            {
                if (_reservations.Values.Any(r => r.Username == username)) 
                    return false;
            }
            else
            {
                if (!user.ClientIds.Contains(clientId))
                    return false;
            }

            foreach (var other in _userStore.GetAllUsers())
            {
                if (other.Username == username ||
                    other.ClientIds.Count == 0)
                    continue;

                if (other.ClientIds.Contains(clientId))
                    return false;
            }

            reservationId = Guid.NewGuid();

            _reservations.Add(
                clientId,
                new Reservation(
                    reservationId,
                    username,
                    DateTimeOffset.UtcNow));

            return true;
        }
    }

    public bool Confirm(string clientId, Guid reservationId)
    {
        lock (_lock)
        {
            if (!TryGetReservation(
                clientId, reservationId, out var reservation))
                return false;

            if (reservation.Expired ||
                IsExpired(reservation, DateTimeOffset.UtcNow))
            {
                reservation.Expired = true;
                return false;
            }

            if (reservation.Confirmed)
                return true;

            reservation.Confirmed = true;
            return true;
        }
    }

    public bool IsActive(string clientId, Guid reservationId)
    {
        lock (_lock)
        {
            return TryGetReservation(clientId, reservationId, out var reservation)
                && reservation.Confirmed;
        }
    }

    public void Release(string clientId, Guid reservationId)
    {
        lock (_lock)
        {
            if (TryGetReservation(clientId, reservationId, out _))
                _reservations.Remove(clientId);
        }
    }

    private bool TryGetReservation(string clientId, Guid reservationId, out Reservation reservation)
    {
        if (_reservations.TryGetValue(clientId, out var found) &&
            found.Id == reservationId)
        {
            reservation = found;
            return true;
        }

        reservation = null!;
        return false;
    }

    private bool IsExpired(Reservation reservation, DateTimeOffset now)
    {
        if (reservation.Confirmed)
            return false;

        return now - reservation.CreatedAt >=
               _options.PendingLifetime;
    }

    private void MarkExpiredPending()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var reservation in _reservations.Values)
        {
            if (IsExpired(reservation, now))
                reservation.Expired = true;
        }
    }
}
