namespace MqttSecurity.Core.Contracts;

public interface IMqttAuthenticationService
{
    bool Authenticate(string username, string password);
}
