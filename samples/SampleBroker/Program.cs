using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Server;
using MqttSecurity.Core.Extensions;
using MqttSecurity.MqttNet.Extensions;

using var services = new ServiceCollection()
    .AddMqttSecurity(options =>
    {
        options.UsersFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "users.json");
    })
    .BuildServiceProvider();

var factory = new MqttServerFactory();

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
    .WithDefaultEndpointBoundIPV6Address(IPAddress.None)
    .WithDefaultEndpointPort(1883)
    .Build();

using var server = factory.CreateMqttServer(options);

await server.UseMqttSecurityAsync(services);

await server.StartAsync();

Console.WriteLine("Broker started on 127.0.0.1:1883");
Console.ReadLine();

await server.StopAsync();