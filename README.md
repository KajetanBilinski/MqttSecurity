# MqttSecurity

[![CI](https://github.com/KajetanBilinski/MqttSecurity/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/KajetanBilinski/MqttSecurity/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A C# library for managing user accounts and topic permissions in an MQTTnet broker.

## Why I built it

While working with MQTTnet, I needed a way to manage accounts and control which topics each account could read from or write to. I started this project to solve that problem and to learn more about C# and MQTT security along the way.

#### IMPORTANT
The library is still in development.

## What it does

- Checks the username and password when a client connects.
- Stores password hashes instead of plain passwords in the broker's user file.
- Lets each account have separate permissions for publishing and subscribing.
- Checks which Client IDs an account can use.
- Temporarily blocks accounts and IP addresses after too many failed logins.
- Includes a tool for generating the user configuration and a sample broker.

For example, a device account can send messages to `devices/01/status` and receive commands from `devices/01/commands/#`. A separate monitoring account can read device messages without permission to publish them.

## Try it locally

You need a .NET 10 SDK. Run these commands from the repository root.

First, generate the sample's user file:

```sh
dotnet run --project tools/MqttSecurity.ConfigGenerator -- users.setup.json samples/SampleBroker/users.json
```

The generator reads usernames, passwords and permissions from `users.setup.json`. It creates `users.json` with password hashes, which the broker uses to check logins. It will not overwrite an existing output file.

Then start the broker:

```sh
dotnet run --project samples/SampleBroker
```

Connect an MQTT client with these settings:

| Setting | Value |
| --- | --- |
| Server | `127.0.0.1` |
| Port | `1883` |
| Username and password | An account from `users.json` |
| Client ID | Use an ID listed in `users.json`, or any nonempty ID if the list is empty. |
| Clean session | Enabled |
| Session expiry interval | `0` |

Press Enter in the broker console to stop it.

The included passwords are examples for local testing. The sample does not encrypt the connection. For network use, configure TLS in MQTTnet and use your own credentials.

## Accounts and permissions

This is an example input for the configuration generator:

```json
[
  {
    "username": "device01",
    "password": "replace-with-your-own-password",
    "permissions": {
      "publish": ["devices/01/status"],
      "subscribe": ["devices/01/commands/#"]
    }
  }
]
```

This account can publish to `devices/01/status` and subscribe to topics under `devices/01/commands/`. The `#` allows the remaining topic levels, so `devices/01/commands/restart` fits this permission.

The generated file contains `passwordHash` instead of `password`. Keep real passwords and user configuration out of Git.

This is an example output for the configuration generator:

```json
[
  {
    "username": "device01",
    "passwordHash": "generated-hash",
    "clientIds": [],
    "permissions": {
        "publish": ["devices/01/status"],
        "subscribe": ["devices/01/commands/#"]
    }
  }
]
```

## Client IDs

A Client ID identifies an MQTT connection. It is separate from the username.

In the generated user file, an empty `clientIds` list means the account can have one reserved or active Client ID at a time. You can also edit this list to allow only specific IDs. Different IDs on that list can connect at the same time.

The generator currently creates an empty list. It does not yet accept a Client ID list in its input.

## Failed logins

By default, the library blocks an account after 5 failed logins within one minute, or an IP address after 20. The block lasts two minutes.

The account and IP limits are separate. A successful login clears the account's failed attempts, but not the IP's. The counters are kept in memory and reset when the broker process restarts.

## Tests

To build the project and run the tests:

```sh
dotnet build MqttSecurity.slnx --configuration Release
dotnet test tests/MqttSecurity.Tests/MqttSecurity.Tests.csproj --configuration Release --no-build
```

The tests cover topic matching, Client ID rules and failed-login protection. Some tests start a local broker and connect MQTT clients to check their behavior together.

## What still needs work

- A connection that fails after reserving a Client ID can leave the account blocked.
- Configuration validation and the generator need further improvements.
- Changing permissions does not automatically remove existing subscriptions.

A complete working example is in [SampleBroker](samples/SampleBroker/Program.cs).

## License

[MIT](LICENSE).
