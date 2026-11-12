using System.Net;
using Microsoft.Extensions.Options;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Protection;

public sealed class MqttLoginProtectionService : IMqttLoginProtectionService
{
    private sealed class FailureState
    {
        public int FailureCount { get; set; }

        public DateTimeOffset WindowStartedAt { get; set; }

        public DateTimeOffset? BlockedUntil { get; set; }
    }

    private readonly object _sync = new();

    private readonly Dictionary<string, FailureState> _accounts = new();

    private readonly Dictionary<IPAddress, FailureState> _addresses = new();

    private readonly MqttLoginProtectionOptions _options;
    private readonly TimeProvider _timeProvider;

    private DateTimeOffset _nextCleanupAt =
        DateTimeOffset.MinValue;

    public MqttLoginProtectionService(
        IOptions<MqttSecurityOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value.LoginProtection;
        _timeProvider = timeProvider;

        if (_options.MaxFailuresPerAccount <= 0 ||
            _options.MaxFailuresPerIpAddress <= 0 ||
            _options.FailureWindow <= TimeSpan.Zero ||
            _options.BlockDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Login protection limits and durations must be positive.",
                nameof(options));
        }
    }

    public MqttLoginProtectionDecision Check(
        string username,
        IPAddress ipAddress)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(ipAddress);

        if (!_options.Enabled)
            return new(true, TimeSpan.Zero);

        ipAddress = NormalizeAddress(ipAddress);

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            CleanupIfNeeded(now);

            var accountDelay = GetRemainingBlock(
                _accounts, username, now);

            var addressDelay = GetRemainingBlock(
                _addresses, ipAddress, now);

            var retryAfter = accountDelay > addressDelay
                ? accountDelay
                : addressDelay;

            return new(
                IsAllowed: retryAfter == TimeSpan.Zero,
                RetryAfter: retryAfter);
        }
    }

    public void RegisterFailure(string username, IPAddress ipAddress)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(ipAddress);

        if (!_options.Enabled)
            return;

        ipAddress = NormalizeAddress(ipAddress);

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            CleanupIfNeeded(now);

            RegisterFailureFor(
                _accounts,
                username,
                now);

            RegisterFailureFor(
                _addresses,
                ipAddress,
                now);
        }
    }

    public void RegisterSuccess(string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        if (!_options.Enabled)
            return;

        lock (_sync)
        {
            _accounts.Remove(username);
        }
    }

    private void RegisterFailureFor(Dictionary<string, FailureState> states, 
        string key, DateTimeOffset now)
    {
        bool found = states.TryGetValue(key, out var state);

        if (!found || IsExpired(state!, now))
        {
            state = new FailureState
            {
                WindowStartedAt = now
            };

            states[key] = state;
        }

        if (state!.BlockedUntil.HasValue)
        {
            DateTimeOffset blockedUntil = state.BlockedUntil.Value;

            if (blockedUntil > now)
            {
                return;
            }
        }

        state.FailureCount++;

        if (state.FailureCount >= _options.MaxFailuresPerAccount)
        {
            state.BlockedUntil = now + _options.BlockDuration;
        }
    }

    private void RegisterFailureFor(Dictionary<IPAddress, FailureState> states,
        IPAddress key, DateTimeOffset now)
    {
        bool found = states.TryGetValue(key, out var state);

        if (!found || IsExpired(state!, now))
        {
            state = new FailureState
            {
                WindowStartedAt = now
            };

            states[key] = state;
        }

        if (state!.BlockedUntil.HasValue)
        {
            DateTimeOffset blockedUntil = state.BlockedUntil.Value;

            if (blockedUntil > now)
            {
                return;
            }
        }

        state.FailureCount++;

        if (state.FailureCount >= _options.MaxFailuresPerIpAddress)
        {
            state.BlockedUntil = now + _options.BlockDuration;
        }
    }

    private static TimeSpan GetRemainingBlock(Dictionary<string, FailureState> states,
            string key, DateTimeOffset now)
    {
        if (!states.TryGetValue(key, out var state))
        {
            return TimeSpan.Zero;
        }

        if (!state.BlockedUntil.HasValue)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset blockedUntil = state.BlockedUntil.Value;

        if (blockedUntil <= now)
        {
            return TimeSpan.Zero;
        }

        return blockedUntil - now;
    }
    private static TimeSpan GetRemainingBlock(Dictionary<IPAddress, FailureState> states,
        IPAddress key, DateTimeOffset now)
    {
        if (!states.TryGetValue(key, out var state))
        {
            return TimeSpan.Zero;
        }

        if (!state.BlockedUntil.HasValue)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset blockedUntil = state.BlockedUntil.Value;

        if (blockedUntil <= now)
        {
            return TimeSpan.Zero;
        }

        return blockedUntil - now;
    }

    private bool IsExpired(FailureState state, DateTimeOffset now)
    {
        if (state.BlockedUntil.HasValue)
        {
            DateTimeOffset blockedUntil = state.BlockedUntil.Value;

            return now >= blockedUntil;
        }

        TimeSpan elapsed = now - state.WindowStartedAt;

        return elapsed >= _options.FailureWindow;
    }

    private void CleanupIfNeeded(DateTimeOffset now)
    {
        if (now < _nextCleanupAt)
            return;

        RemoveExpired(_accounts, now);
        RemoveExpired(_addresses, now);

        _nextCleanupAt = now + TimeSpan.FromMinutes(1);
    }

    private void RemoveExpired(Dictionary<string, FailureState> states,
        DateTimeOffset now)
    {
        var expiredKeys = new List<string>();

        foreach (var entry in states)
        {
            if (IsExpired(entry.Value, now))
            {
                expiredKeys.Add(entry.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            states.Remove(key);
        }
    }

    private void RemoveExpired(Dictionary<IPAddress, FailureState> states,
        DateTimeOffset now)
    {
        var expiredKeys = new List<IPAddress>();

        foreach (var entry in states)
        {
            if (IsExpired(entry.Value, now))
            {
                expiredKeys.Add(entry.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            states.Remove(key);
        }
    }

    private static IPAddress NormalizeAddress(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
    }
}