namespace MqttSecurity.Core.Contracts;

internal interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
