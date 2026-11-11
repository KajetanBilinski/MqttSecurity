using Microsoft.Extensions.DependencyInjection;
using MqttSecurity.ConfigGenerator.Services;
using MqttSecurity.Core.Contracts;
using MqttSecurity.Core.Extensions;

if (args.Length != 2)
{
    Console.WriteLine(
        "Usage: MqttSecurity.ConfigGenerator <setup.json> <users.json>");

    return 1;
}

string inputPath = args[0];
string outputPath = args[1];

try
{
    using var services = new ServiceCollection()
        .AddMqttSecurity(options =>
        {
            options.UsersFilePath = outputPath;
        })
        .BuildServiceProvider();

    var hasher = services.GetRequiredService<IPasswordHasher>();

    var generator = new MqttConfigurationGenerator(hasher);

    int usersCount = await generator.GenerateAsync(
        inputPath,
        outputPath);

    Console.WriteLine(
        $"Successfully generated {usersCount} users.");

    Console.WriteLine(
        $"Output: {Path.GetFullPath(outputPath)}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"Configuration generation failed: {ex.Message}");

    return 1;
}
