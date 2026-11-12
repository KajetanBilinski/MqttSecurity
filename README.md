# MqttSecurity

[![CI](https://github.com/KajetanBilinski/MqttSecurity/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/KajetanBilinski/MqttSecurity/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Authentication, topic authorization and login protection for MQTTnet brokers, built with C# and .NET 10.

**Status: preview.** The project is under development and its public API may change before 1.0. Packages are built as GitHub Actions artifacts; automatic NuGet publication is not configured.

## Features

- Username/password authentication using ASP.NET Core Identity password hashing.
- Separate publish and subscribe permissions with MQTT `+` and `#` filters.
- Client ID allowlists and connection reservations.
- Temporary account and IP blocks after failed login attempts.
- JSON-backed user configuration with explicit reload support.
- Dependency injection and MQTTnet server integration.
- A command-line configuration generator and a runnable sample broker.

## Repository structure

| Directory | Purpose |
| --- | --- |
| `src/MqttSecurity.Core` | Authentication, authorization, configuration, storage and login protection |
| `src/MqttSecurity.MqttNet` | MQTTnet server integration and DI registration |
| `tools/MqttSecurity.ConfigGenerator` | Converts setup files containing passwords into hashed user configuration |
| `samples/SampleBroker` | Local broker listening on `127.0.0.1:1883` |
| `tests/MqttSecurity.Tests` | Unit tests and broker integration tests |
| `.github/workflows` | CI and versioned package builds |

## Requirements

- A stable .NET 10 SDK.
- MQTTnet.Server `5.2.0.1603`, referenced by the integration project.

`global.json` selects a stable SDK from the .NET 10.0 line. `Directory.Build.props` provides shared version and package metadata. Run the commands below from the repository root and explicitly target `MqttSecurity.slnx`.

## Build and test

```sh
dotnet restore MqttSecurity.slnx
dotnet build MqttSecurity.slnx --configuration Release --no-restore
dotnet test tests/MqttSecurity.Tests/MqttSecurity.Tests.csproj --configuration Release --no-build --no-restore
```

Integration tests open local TCP sockets. The test environment must allow loopback connections.

## Run the sample

The tracked `users.setup.json` contains demonstration credentials only. Never use them in a deployment.

Generate the sample's user configuration:

```sh
dotnet run --project tools/MqttSecurity.ConfigGenerator -- users.setup.json samples/SampleBroker/users.json
```

The generator refuses to overwrite an existing output file. If one already exists, choose another output path or deliberately replace your local sample configuration.

Start the broker:

```sh
dotnet run --project samples/SampleBroker
```

Connect an MQTT client to `127.0.0.1:1883`, using a username and password from the setup file and a nonempty, unique Client ID. With Client ID binding enabled, use a clean session and a session expiry interval of zero. Press Enter in the broker console to stop it.

The sample endpoint uses plain TCP. Configure TLS through MQTTnet before exposing a broker to a network.

## Use the library

Reference `src/MqttSecurity.MqttNet/MqttSecurity.MqttNet.csproj` from your broker application. It references Core transitively. Built `.nupkg` files are also available as workflow artifacts once CI has run successfully.

```csharp
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Server;
using MqttSecurity.Core.Extensions;
using MqttSecurity.MqttNet.Extensions;

using var services = new ServiceCollection()
    .AddMqttSecurity(options =>
    {
        options.UsersFilePath = Path.Combine(
            AppContext.BaseDirectory, "users.json");
        options.LoginProtection.MaxFailuresPerAccount = 5;
        options.LoginProtection.MaxFailuresPerIpAddress = 20;
    })
    .BuildServiceProvider();

var options = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
    .WithDefaultEndpointBoundIPV6Address(IPAddress.None)
    .WithDefaultEndpointPort(1883)
    .Build();

using var server = new MqttServerFactory().CreateMqttServer(options);
await server.UseMqttSecurityAsync(services);
await server.StartAsync();
Console.ReadLine();
await server.StopAsync();
```

`UseMqttSecurityAsync` loads and validates the user file before attaching handlers. Call it once, before starting the server.

If the application needs additional connection checks, pass an asynchronous `additionalValidation` callback to `UseMqttSecurityAsync`. It runs after authentication and session checks, before Client ID reservation. Reject by setting `context.ReasonCode` to an appropriate failure code. Do not append connection validators after this integration: delays, rejections or exceptions there bypass the intended reservation lifecycle.

## User configuration

The setup file accepted by the generator has this structure:

```json
[
  {
    "username": "device01",
    "password": "replace-with-a-unique-password",
    "permissions": {
      "publish": ["devices/01/status", "devices/01/diagnostics/#"],
      "subscribe": ["devices/01/commands/#"]
    }
  }
]
```

The generated runtime file uses `passwordHash` instead of `password`, and includes a `clientIds` array. An empty `clientIds` array allows one reserved/active Client ID per account. A nonempty array restricts the account to the listed IDs, which may connect independently.

The generator currently emits an empty Client ID list and does not accept an input allowlist. Configure explicit IDs in the generated runtime file if needed. At least one publish or subscribe permission is required by runtime validation.

`+` matches one topic level; `#` matches remaining levels. Subscription authorization checks whether an allowed filter covers the entire requested filter. Topics and filters starting with `$`, including shared-subscription syntax, are currently rejected.

Keep real setup files containing plaintext passwords out of Git. Treat password hashes as sensitive configuration too. Generated `users.json` files in the root and sample directory are ignored by Git.

## Default settings

| Setting | Default |
| --- | --- |
| `UsersFilePath` | Required; no usable default |
| `EnableClientIdBinding` | `true` |
| `PendingLifetime` | 1 minute |
| `LoginProtection.Enabled` | `true` |
| `LoginProtection.MaxFailuresPerAccount` | 5 |
| `LoginProtection.MaxFailuresPerIpAddress` | 20 |
| `LoginProtection.FailureWindow` | 1 minute |
| `LoginProtection.BlockDuration` | 2 minutes |

Successful password authentication clears the account failure counter, but not the IP counter. Blocked attempts are rejected before password hash verification. State is local to one service instance and is lost on process restart.

## License

[MIT](LICENSE).
