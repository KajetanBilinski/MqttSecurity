namespace MqttSecurity.Core.Contracts;

internal interface IMqttAuthenticationService
{
    bool Authenticate(string username, string password);
}
