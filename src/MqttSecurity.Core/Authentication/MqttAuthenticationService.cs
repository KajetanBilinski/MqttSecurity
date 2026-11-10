using MqttSecurity.Core.Contracts;

namespace MqttSecurity.Core.Authentication;

internal class MqttAuthenticationService : IMqttAuthenticationService
{
    private readonly IMqttUserStore _userStore;
    private readonly IPasswordHasher _passwordHasher;

    public MqttAuthenticationService(IMqttUserStore userStore, IPasswordHasher passwordHasher)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
    }
    public bool Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password))
            return false;

        var user = _userStore.GetUser(username);

        if (user is null) return false;

        return _passwordHasher.VerifyPassword(
            password,
            user.PasswordHash);
    }
}
