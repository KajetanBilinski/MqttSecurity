using System.Net;
using Microsoft.Extensions.Options;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Protection;

namespace MqttSecurity.Tests;

public sealed class MqttLoginProtectionServiceTests
{
    private static readonly IPAddress FirstIp = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress SecondIp = IPAddress.Parse("192.0.2.2");

    [Fact]
    public void Check_AccountLimit_BlocksAccountAcrossAddressesOnly()
    {
        var service = CreateService(new TestClock());

        for (int i = 0; i < 4; i++)
            service.RegisterFailure("device01", FirstIp);

        Assert.True(service.Check("device01", FirstIp).IsAllowed);
        service.RegisterFailure("device01", FirstIp);

        var decision = service.Check("device01", SecondIp);
        Assert.False(decision.IsAllowed);
        Assert.Equal(TimeSpan.FromMinutes(2), decision.RetryAfter);
        Assert.True(service.Check("device02", FirstIp).IsAllowed);
    }

    [Fact]
    public void Check_IpLimit_BlocksDifferentAccountsOnSameAddress()
    {
        var service = CreateService(new TestClock());

        for (int i = 0; i < 19; i++)
            service.RegisterFailure($"device-{i}", FirstIp);

        Assert.True(service.Check("other", FirstIp).IsAllowed);
        service.RegisterFailure("last", FirstIp);

        Assert.False(service.Check("other", FirstIp).IsAllowed);
        Assert.True(service.Check("other", SecondIp).IsAllowed);
    }

    [Fact]
    public void Check_BlockExpiresAtExactDeadline_AndNextFailureStartsFresh()
    {
        var clock = new TestClock();
        var service = CreateService(clock);
        for (int i = 0; i < 5; i++)
            service.RegisterFailure("device01", FirstIp);

        clock.Advance(TimeSpan.FromMinutes(2) - TimeSpan.FromTicks(1));
        var decision = service.Check("device01", FirstIp);
        Assert.False(decision.IsAllowed);
        Assert.Equal(TimeSpan.FromTicks(1), decision.RetryAfter);

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.True(service.Check("device01", FirstIp).IsAllowed);
        Assert.Equal(TimeSpan.Zero, service.Check("device01", FirstIp).RetryAfter);

        service.RegisterFailure("device01", FirstIp);
        Assert.True(service.Check("device01", FirstIp).IsAllowed);
    }

    [Fact]
    public void RegisterFailure_AtWindowBoundary_StartsNewCounter()
    {
        var clock = new TestClock();
        var service = CreateService(clock);
        for (int i = 0; i < 4; i++)
            service.RegisterFailure("device01", FirstIp);

        clock.Advance(TimeSpan.FromMinutes(1));
        service.RegisterFailure("device01", FirstIp);

        Assert.True(service.Check("device01", FirstIp).IsAllowed);
    }

    [Fact]
    public void Check_WindowExpiration_DoesNotRemoveActiveBlock()
    {
        var clock = new TestClock();
        var service = CreateService(clock);
        for (int i = 0; i < 5; i++)
            service.RegisterFailure("device01", FirstIp);

        clock.Advance(TimeSpan.FromMinutes(1));

        var decision = service.Check("device01", FirstIp);
        Assert.False(decision.IsAllowed);
        Assert.Equal(TimeSpan.FromMinutes(1), decision.RetryAfter);
    }

    [Fact]
    public void RegisterFailure_DuringBlock_DoesNotExtendDeadline()
    {
        var clock = new TestClock();
        var service = CreateService(clock);
        for (int i = 0; i < 5; i++)
            service.RegisterFailure("device01", FirstIp);

        clock.Advance(TimeSpan.FromSeconds(30));
        service.RegisterFailure("device01", FirstIp);

        Assert.Equal(TimeSpan.FromSeconds(90),
            service.Check("device01", SecondIp).RetryAfter);
    }

    [Fact]
    public void RegisterSuccess_ResetsAccountCounter()
    {
        var service = CreateService(new TestClock());
        for (int i = 0; i < 4; i++)
            service.RegisterFailure("device01", FirstIp);

        service.RegisterSuccess("device01");
        service.RegisterFailure("device01", FirstIp);

        Assert.True(service.Check("device01", SecondIp).IsAllowed);
    }

    [Fact]
    public void RegisterSuccess_DoesNotResetIpCounter()
    {
        var service = CreateService(new TestClock());
        for (int i = 0; i < 19; i++)
            service.RegisterFailure($"device-{i}", FirstIp);

        service.RegisterSuccess("device-0");
        service.RegisterFailure("last", FirstIp);

        Assert.False(service.Check("new-device", FirstIp).IsAllowed);
    }

    [Fact]
    public void Check_BothBlocked_ReturnsLongerRemainingDuration()
    {
        var clock = new TestClock();
        var service = CreateService(clock);
        for (int i = 0; i < 5; i++)
            service.RegisterFailure("device01", FirstIp);

        clock.Advance(TimeSpan.FromSeconds(30));
        for (int i = 0; i < 15; i++)
            service.RegisterFailure($"other-{i}", FirstIp);

        Assert.Equal(TimeSpan.FromSeconds(90),
            service.Check("device01", SecondIp).RetryAfter);
        Assert.Equal(TimeSpan.FromMinutes(2),
            service.Check("device01", FirstIp).RetryAfter);
    }

    [Fact]
    public void Check_MappedIpv6Address_SharesIpv4Counter()
    {
        var service = CreateService(new TestClock());
        for (int i = 0; i < 20; i++)
        {
            var address = i % 2 == 0 ? FirstIp : FirstIp.MapToIPv6();
            service.RegisterFailure($"device-{i}", address);
        }

        Assert.False(service.Check("other", FirstIp).IsAllowed);
        Assert.False(service.Check("other", FirstIp.MapToIPv6()).IsAllowed);
    }

    [Fact]
    public void Check_ProtectionDisabled_DoesNotBlock()
    {
        var service = CreateService(new TestClock(), enabled: false);
        for (int i = 0; i < 30; i++)
            service.RegisterFailure("device01", FirstIp);

        var decision = service.Check("device01", FirstIp);
        Assert.True(decision.IsAllowed);
        Assert.Equal(TimeSpan.Zero, decision.RetryAfter);
    }

    [Fact]
    public void RegisterFailure_ConcurrentFailuresForDifferentAccounts_BlockIp()
    {
        var service = CreateService(new TestClock());

        Parallel.For(0, 20, i =>
            service.RegisterFailure($"device-{i}", FirstIp));

        Assert.False(service.Check("other", FirstIp).IsAllowed);
        Assert.True(service.Check("other", SecondIp).IsAllowed);
    }

    private static MqttLoginProtectionService CreateService(
        TestClock clock,
        bool enabled = true)
    {
        var options = Options.Create(new MqttSecurityOptions
        {
            LoginProtection = new MqttLoginProtectionOptions
            {
                Enabled = enabled,
                MaxFailuresPerAccount = 5,
                MaxFailuresPerIpAddress = 20,
                FailureWindow = TimeSpan.FromMinutes(1),
                BlockDuration = TimeSpan.FromMinutes(2)
            }
        });

        return new MqttLoginProtectionService(options, clock);
    }

    // Kontrolowany zegar: przesuwamy czas bez Thread.Sleep i Task.Delay.
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now =
            new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration)
        {
            _now += duration;
        }
    }
}
