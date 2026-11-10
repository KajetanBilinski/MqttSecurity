namespace MqttSecurity.Core.Contracts;

public interface IMqttAuthorizationService
{
    bool CanPublish(string username, string topic);
    bool CanSubscribe(string username, string topicFilter);
}
