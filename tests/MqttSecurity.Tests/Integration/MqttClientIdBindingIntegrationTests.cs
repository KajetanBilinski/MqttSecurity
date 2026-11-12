
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Extensions;
using MqttSecurity.MqttNet.Extensions;
using Xunit;

namespace MqttSecurity.Tests.Integration;

[CollectionDefinition("MQTT Integration", DisableParallelization = true)]
public sealed class MqttIntegrationCollection;

[Collection("MQTT Integration")]
public sealed class MqttClientIdBindingIntegrationTests
{
    [Fact]
    public async Task Connect_SessionItems_ContainSameReservationId()
    {
        await using var broker = await TestBroker.StartAsync(
            [("device01", [])]);

        using var client = CreateClient();

        var result = await client.ConnectAsync(
            ClientOptions(broker.Port, "device01", "train-001"));

        Assert.Equal(MqttClientConnectResultCode.Success,
            result.ResultCode);

        await broker.WaitUntilAsync(() =>
            broker.Connected.ContainsKey("train-001"));

        Assert.True(broker.Validating.TryGetValue(
            "train-001", out var validatingId));

        Assert.True(broker.Connected.TryGetValue(
            "train-001", out var connectedId));

        Assert.NotEqual(Guid.Empty, validatingId);
        Assert.Equal(validatingId, connectedId);

        Assert.True(broker.Binding.IsActive(
            "train-001", connectedId));

        await client.DisconnectAsync();

        await broker.WaitUntilAsync(() =>
            broker.Disconnected.ContainsKey("train-001") &&
            !broker.Binding.IsActive("train-001", connectedId));

        Assert.Equal(
            connectedId,
            broker.Disconnected["train-001"]);
    }

    [Fact]
    public async Task Disconnect_ReleasesAccount_WithoutWaitingForTimeout()
    {
        await using var broker = await TestBroker.StartAsync(
            [("device01", [])]);

        using var first = CreateClient();
        using var second = CreateClient();

        await AssertConnectedAsync(first,
            ClientOptions(broker.Port, "device01", "train-001"));

        var id = broker.Validating["train-001"];

        await first.DisconnectAsync();

        await broker.WaitUntilAsync(() =>
            !broker.Binding.IsActive("train-001", id) &&
            broker.Disconnected.ContainsKey("train-001"));

        await AssertConnectedAsync(second,
            ClientOptions(broker.Port, "device01", "train-002"));
    }

    [Fact]
    public async Task ActiveAccount_RejectsSecondClientId()
    {
        await using var broker = await TestBroker.StartAsync(
            [("device01", [])]);

        using var first = CreateClient();
        using var second = CreateClient();

        await AssertConnectedAsync(first,
            ClientOptions(broker.Port, "device01", "train-001"));

        await AssertRejectedAsync(second,
            ClientOptions(broker.Port, "device01", "train-002"));

        Assert.True(first.IsConnected);
    }

    [Fact]
    public async Task ExplicitClientIds_AllowListedConnectionsOnly()
    {
        await using var broker = await TestBroker.StartAsync(
            [("device01", ["train-001", "train-002"])]);

        using var first = CreateClient();
        using var second = CreateClient();
        using var third = CreateClient();

        await AssertConnectedAsync(first,
            ClientOptions(broker.Port, "device01", "train-001"));

        await AssertConnectedAsync(second,
            ClientOptions(broker.Port, "device01", "train-002"));

        await AssertRejectedAsync(third,
            ClientOptions(broker.Port, "device01", "train-003"));

        Assert.True(first.IsConnected);
        Assert.True(second.IsConnected);
    }

    [Theory]
    [InlineData(false, 0u)]
    [InlineData(true, 60u)]
    public async Task Connect_PersistentSession_IsRejected(
        bool cleanSession,
        uint expiry)
    {
        await using var broker = await TestBroker.StartAsync(
            [("device01", [])]);

        using var client = CreateClient();

        await AssertRejectedAsync(client,
            ClientOptions(
                broker.Port,
                "device01",
                "train-001",
                cleanSession,
                expiry));
    }


    [Fact]
    public async Task RejectedHandshake_BlocksAccountUntilReleased()
    {
        await using var broker = await TestBroker.StartAsync(
            [("device01", [])],
            pendingLifetime: TimeSpan.FromMilliseconds(300),
            rejectAfterReservation: true);

        using var first = CreateClient();

        await AssertRejectedAsync(first,
            ClientOptions(broker.Port, "device01", "broken"));

        Assert.True(broker.Validating.TryGetValue(
            "broken", out var oldId));

        await Task.Delay(650);

        using var second = CreateClient();

        await AssertRejectedAsync(second,
            ClientOptions(broker.Port, "device01", "train-002"));

        Assert.False(broker.Binding.Confirm("broken", oldId));

        broker.Binding.Release("broken", oldId);

        using var third = CreateClient();

        await AssertConnectedAsync(third,
            ClientOptions(broker.Port, "device01", "train-002"));
    }


    private static IMqttClient CreateClient()
        => new MqttClientFactory().CreateMqttClient();

    private static MqttClientOptions ClientOptions(
        int port,
        string username,
        string clientId,
        bool cleanSession = true,
        uint sessionExpiry = 0)
    {
        return new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", port)
            .WithClientId(clientId)
            .WithCredentials(username, TestBroker.Password)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanSession(cleanSession)
            .WithSessionExpiryInterval(sessionExpiry)
            .Build();
    }

    private static async Task AssertConnectedAsync(
        IMqttClient client,
        MqttClientOptions options)
    {
        var result = await client.ConnectAsync(options)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(MqttClientConnectResultCode.Success,
            result.ResultCode);

        Assert.True(client.IsConnected);
    }

    private static async Task AssertRejectedAsync(
        IMqttClient client,
        MqttClientOptions options)
    {
        var result = await client.ConnectAsync(options)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotEqual(MqttClientConnectResultCode.Success,
            result.ResultCode);

        Assert.False(client.IsConnected);
    }

    private sealed class TestBroker : IAsyncDisposable
    {
        public const string Password = "TestPassword123!";

        private readonly MqttServer _server;
        private readonly ServiceProvider _services;
        private readonly string _directory;

        public int Port { get; }

        public IMqttClientIdBindingService Binding =>
            _services.GetRequiredService<IMqttClientIdBindingService>();

        public ConcurrentDictionary<string, Guid> Validating { get; } = new();
        public ConcurrentDictionary<string, Guid> Connected { get; } = new();
        public ConcurrentDictionary<string, Guid> Disconnected { get; } = new();

        private TestBroker(
            MqttServer server,
            ServiceProvider services,
            string directory,
            int port)
        {
            _server = server;
            _services = services;
            _directory = directory;
            Port = port;
        }

        public static async Task<TestBroker> StartAsync(
            (string Username, string[] ClientIds)[] users,
            TimeSpan? pendingLifetime = null,
            bool rejectAfterReservation = false)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"mqttsec-test-{Guid.NewGuid():N}");

            Directory.CreateDirectory(directory);

            var usersPath = Path.Combine(directory, "users.json");

            var services = new ServiceCollection()
                .AddMqttSecurity(options =>
                {
                    options.UsersFilePath = usersPath;
                    options.EnableClientIdBinding = true;
                    options.PendingLifetime = pendingLifetime
                        ?? TimeSpan.FromSeconds(60);
                })
                .BuildServiceProvider();

            var hasher = services.GetRequiredService<IPasswordHasher>();
            var hash = hasher.HashPassword(Password);

            // Produkcyjny format users.json, bez pola Enabled.
            var jsonUsers = users.Select(user => new
            {
                username = user.Username,
                passwordHash = hash,
                clientIds = user.ClientIds,
                permissions = new
                {
                    publish = new[] { "test/#" },
                    subscribe = new[] { "test/#" }
                }
            });

            await File.WriteAllTextAsync(
                usersPath,
                JsonSerializer.Serialize(jsonUsers));

            int port = GetFreeTcpPort();

            var serverOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                .WithDefaultEndpointBoundIPV6Address(IPAddress.None)
                .WithDefaultEndpointPort(port)
                .WithPersistentSessions(false)
                .Build();

            var server = new MqttServerFactory()
                .CreateMqttServer(serverOptions);

            var host = new TestBroker(
                server, services, directory, port);

            await server.UseMqttSecurityAsync(services);

            if (rejectAfterReservation)
            {
                server.ValidatingConnectionAsync += context =>
                {
                    if (context.ClientId == "broken")
                    {
                        context.ReasonCode =
                            MqttConnectReasonCode.NotAuthorized;
                    }

                    return Task.CompletedTask;
                };
            }

            server.ValidatingConnectionAsync += context =>
            {
                SaveGuid(context.ClientId,
                    context.SessionItems, host.Validating);
                return Task.CompletedTask;
            };

            server.ClientConnectedAsync += context =>
            {
                SaveGuid(context.ClientId,
                    context.SessionItems, host.Connected);
                return Task.CompletedTask;
            };

            server.ClientDisconnectedAsync += context =>
            {
                SaveGuid(context.ClientId,
                    context.SessionItems, host.Disconnected);
                return Task.CompletedTask;
            };

            await server.StartAsync();

            return host;
        }

        private static void SaveGuid(
            string clientId,
            System.Collections.IDictionary items,
            ConcurrentDictionary<string, Guid> target)
        {
            var id = items.Values
                .Cast<object>()
                .OfType<Guid>()
                .SingleOrDefault();

            if (id != Guid.Empty)
                target[clientId] = id;
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            listener.Stop();
            return port;
        }

        public async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(25);

            Assert.True(condition(), "Condition not met within 5 seconds.");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _server.StopAsync();
            }
            finally
            {
                _server.Dispose();
                _services.Dispose();
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
