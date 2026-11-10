using Microsoft.AspNetCore.Identity;
using MqttSecurity.Core.Contracts;

namespace MqttSecurity.Core.Authentication;

internal class MqttPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _passwordHasher;
    private readonly object _contextUser;
    public MqttPasswordHasher()
    {
        _passwordHasher = new PasswordHasher<object>();
        _contextUser = new();
    }
    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _passwordHasher.HashPassword(_contextUser, password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return _passwordHasher.VerifyHashedPassword(_contextUser, passwordHash, password)
            != PasswordVerificationResult.Failed;
    }
}
