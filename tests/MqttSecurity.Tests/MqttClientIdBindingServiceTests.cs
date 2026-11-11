
using Microsoft.Extensions.Options;
using Moq;
using MqttSecurity.Core.Authorization;
using MqttSecurity.Core.Configuration;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Models;
using Xunit;

namespace MqttSecurity.Tests.Authorization;

public sealed class MqttClientIdBindingServiceTests
{
    // ==========================================
    // CONFIGURATION
    // ==========================================

    [Fact]
    public void TryReserve_BindingDisabled_AllowsConnection()
    {
        var sut = CreateService([], bindingEnabled: false);

        var result = sut.TryReserve(
            "unknown-user",
            "client-01",
            out var reservationId);

        Assert.True(result);
        Assert.Equal(Guid.Empty, reservationId);
    }

    [Theory]
    [InlineData("", "client-01")]
    [InlineData(" ", "client-01")]
    [InlineData("device01", "")]
    [InlineData("device01", " ")]
    public void TryReserve_InvalidArguments_ReturnsFalse(
        string username,
        string clientId)
    {
        var sut = CreateService([User("device01")]);

        var result = sut.TryReserve(
            username, clientId, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryReserve_UnknownUser_ReturnsFalse()
    {
        var sut = CreateService([User("device01")]);

        Assert.False(sut.TryReserve(
            "unknown", "client-01", out _));
    }

    // ==========================================
    // EMPTY CLIENT IDS
    // ==========================================

    [Fact]
    public void TryReserve_EmptyClientIds_AllowsFirstClient()
    {
        var sut = CreateService([User("device01")]);

        var result = sut.TryReserve(
            "device01", "client-01", out var reservationId);

        Assert.True(result);
        Assert.NotEqual(Guid.Empty, reservationId);
    }

    [Fact]
    public void TryReserve_EmptyClientIds_RejectsSecondClient()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out _));

        Assert.False(sut.TryReserve(
            "device01", "client-02", out _));
    }

    [Fact]
    public void TryReserve_SameClientId_RejectsDuplicate()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out _));

        Assert.False(sut.TryReserve(
            "device01", "client-01", out _));
    }

    [Fact]
    public void TryReserve_AfterRelease_AllowsAnotherClient()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        sut.Release("client-01", reservationId);

        Assert.True(sut.TryReserve(
            "device01", "client-02", out _));
    }

    // ==========================================
    // EXPLICIT CLIENT IDS
    // ==========================================

    [Fact]
    public void TryReserve_ExplicitClientIds_AllowsListedClients()
    {
        var sut = CreateService([
            User("device01", "client-01", "client-02")
        ]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out _));

        Assert.True(sut.TryReserve(
            "device01", "client-02", out _));
    }

    [Fact]
    public void TryReserve_ExplicitClientIds_RejectsUnknownClient()
    {
        var sut = CreateService([
            User("device01", "client-01", "client-02")
        ]);

        Assert.False(sut.TryReserve(
            "device01", "client-03", out _));
    }

    [Fact]
    public void TryReserve_ExplicitClientId_BlocksOtherAccount()
    {
        var sut = CreateService([
            User("device01"),
            User("device02", "reserved-client")
        ]);

        // ClientId przypisany do device02.
        Assert.False(sut.TryReserve(
            "device01", "reserved-client", out _));

        Assert.True(sut.TryReserve(
            "device02", "reserved-client", out _));
    }

    [Fact]
    public void TryReserve_SameClientIdAcrossAccounts_RejectsSecond()
    {
        var sut = CreateService([
            User("device01"),
            User("device02")
        ]);

        Assert.True(sut.TryReserve(
            "device01", "shared-client", out _));

        Assert.False(sut.TryReserve(
            "device02", "shared-client", out _));
    }

    // ==========================================
    // CONFIRM
    // ==========================================

    [Fact]
    public void Confirm_ValidReservation_MarksAsActive()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        Assert.False(sut.IsActive(
            "client-01", reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        Assert.True(sut.IsActive(
            "client-01", reservationId));
    }

    [Fact]
    public void Confirm_InvalidReservationId_ReturnsFalse()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out _));

        Assert.False(sut.Confirm(
            "client-01", Guid.NewGuid()));
    }

    [Fact]
    public void Confirm_UnknownClientId_ReturnsFalse()
    {
        var sut = CreateService([User("device01")]);

        Assert.False(sut.Confirm(
            "unknown-client", Guid.NewGuid()));
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ReturnsTrue()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        Assert.True(sut.IsActive(
            "client-01", reservationId));
    }

    // ==========================================
    // RELEASE
    // ==========================================

    [Fact]
    public void Release_ValidReservation_RemovesReservation()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        sut.Release("client-01", reservationId);

        Assert.False(sut.IsActive(
            "client-01", reservationId));

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var newReservationId));

        Assert.NotEqual(reservationId, newReservationId);
    }

    [Fact]
    public void Release_InvalidReservationId_DoesNotRemoveReservation()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        // Nieprawidłowy GUID nie zwolni połączenia.
        sut.Release("client-01", Guid.NewGuid());

        Assert.True(sut.IsActive(
            "client-01", reservationId));

        Assert.False(sut.TryReserve(
            "device01", "client-02", out _));
    }

    [Fact]
    public void Release_OldReservation_DoesNotRemoveNewReservation()
    {
        var sut = CreateService([User("device01")]);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var oldId));

        sut.Release("client-01", oldId);

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var newId));

        Assert.True(sut.Confirm("client-01", newId));

        // Spóźnione zdarzenie Disconnect poprzedniego klienta.
        sut.Release("client-01", oldId);

        Assert.True(sut.IsActive("client-01", newId));
    }

    // ==========================================
    // TIMEOUT
    // ==========================================

    [Fact]
    public async Task Confirm_ExpiredPending_ReturnsFalse()
    {
        var sut = CreateService(
            [User("device01")],
            pendingLifetime: TimeSpan.FromMilliseconds(100));

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        // Klient nie ukończył nawiązywania połączenia.
        await Task.Delay(300);

        Assert.False(sut.Confirm(
            "client-01", reservationId));

        Assert.False(sut.IsActive(
            "client-01", reservationId));
    }

    [Fact]
    public async Task TryReserve_ExpiredPending_AllowsAnotherClient()
    {
        var sut = CreateService(
            [User("device01")],
            pendingLifetime: TimeSpan.FromMilliseconds(100));

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var oldId));

        await Task.Delay(300);

        // RemoveExpiredPending usuwa poprzednią rezerwację.
        Assert.True(sut.TryReserve(
            "device01", "client-02", out var newId));

        Assert.NotEqual(oldId, newId);

        Assert.True(sut.Confirm(
            "client-02", newId));
    }

    [Fact]
    public async Task ActiveReservation_DoesNotExpire()
    {
        var sut = CreateService(
            [User("device01")],
            pendingLifetime: TimeSpan.FromMilliseconds(100));

        Assert.True(sut.TryReserve(
            "device01", "client-01", out var reservationId));

        Assert.True(sut.Confirm(
            "client-01", reservationId));

        await Task.Delay(300);

        Assert.True(sut.IsActive(
            "client-01", reservationId));

        Assert.False(sut.TryReserve(
            "device01", "client-02", out _));
    }

    // ==========================================
    // CONCURRENCY
    // ==========================================

    [Fact]
    public void TryReserve_ConcurrentClients_OnlyOneWins()
    {
        var sut = CreateService([User("device01")]);

        int accepted = 0;

        Parallel.For(0, 100, i =>
        {
            if (sut.TryReserve(
                "device01",
                $"client-{i}",
                out _))
            {
                Interlocked.Increment(ref accepted);
            }
        });

        Assert.Equal(1, accepted);
    }

    [Fact]
    public void TryReserve_ConcurrentAccounts_SameClientIdOnlyOneWins()
    {
        var sut = CreateService([
            User("device01"),
            User("device02")
        ]);

        int accepted = 0;

        Parallel.For(0, 100, i =>
        {
            string username = i % 2 == 0
                ? "device01"
                : "device02";

            if (sut.TryReserve(
                username,
                "shared-client",
                out _))
            {
                Interlocked.Increment(ref accepted);
            }
        });

        Assert.Equal(1, accepted);
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private static MqttClientIdBindingService CreateService(
        MqttUser[] users,
        bool bindingEnabled = true,
        TimeSpan? pendingLifetime = null)
    {
        var store = new Mock<IMqttUserStore>();

        store.Setup(s => s.GetUser(It.IsAny<string>()))
            .Returns((string username) =>
                users.FirstOrDefault(
                    u => u.Username == username));

        store.Setup(s => s.GetAllUsers())
            .Returns(users);

        var options = Options.Create(new MqttSecurityOptions
        {
            EnableClientIdBinding = bindingEnabled,
            PendingLifetime = pendingLifetime ??
                TimeSpan.FromSeconds(60)
        });

        return new MqttClientIdBindingService(
            store.Object,
            options);
    }

    private static MqttUser User(
        string username,
        params string[] clientIds)
    {
        return new MqttUser
        {
            Username = username,
            PasswordHash = "test-hash",
            ClientIds = clientIds.ToList(),
            Permissions = new MqttPermissions()
        };
    }
}
